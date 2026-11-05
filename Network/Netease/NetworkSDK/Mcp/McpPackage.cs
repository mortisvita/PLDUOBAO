using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Mcp
{
    /// <summary>
    /// .mcp（MCPK）资源包解析器。
    ///
    /// 格式（由 package_builder.go 定义）：
    ///   [0:4]    "MCPK"
    ///   [12:16]  groupTableStart (= 57)
    ///   [16:20]  fileTableStart
    ///   [20:24]  dataStart
    ///   [24:..]  packageId
    ///
    ///   groupTable: 每项 12 字节 (id, offset, count)
    ///   fileTable:  每项 16 字节 (file, offset, size, originSize)，offset 相对 dataStart
    ///   data:       各模块 payload 依次排列
    ///
    /// 单个模块的解码链（encodeModule 的逆过程）：
    ///   1. Rotor 解密（DRegion，6 转子，动态 key）
    ///   2. zlib 解压
    ///   3. 前 130 字节 XOR 0x9C
    ///   4. 整体字节反转
    ///   → 得到 pymarshal 加密的 pyc
    /// </summary>
    internal sealed class McpPackage
    {
        private const int HeaderSize = 57;

        /// <summary>encodeModule 使用的动态密钥（与 Chronal PackageTool 一致）。</summary>
        private static readonly string DynamicKey = BuildDynamicKey();

        private static string BuildDynamicKey()
        {
            const string partA = "j2h56ogodh3sk";
            const string partB = "=dziaq;";
            const string partC = "|`o=5v7!\"-276";

            StringBuilder sb = new();

            for (int i = 0; i < 4; i++) sb.Append(partA);
            for (int i = 0; i < 5; i++) sb.Append(partB).Append(partA).Append(partC);
            sb.Append("$@");
            for (int i = 0; i < 7; i++) sb.Append(partB);
            for (int i = 0; i < 2; i++) sb.Append(partC);
            sb.Append("&]`");

            return sb.ToString();
        }

        public sealed class Entry
        {
            public uint File;
            public uint Offset;
            public uint Size;
            public uint OriginSize;
        }

        private readonly byte[] _data;
        private readonly uint _fileTableStart;
        private readonly uint _dataStart;

        public string PackageId { get; }
        public int GroupCount { get; }
        public int EntryCount => Entries.Count;
        public List<Entry> Entries { get; } = new();

        private McpPackage(byte[] data, uint fileTableStart, uint dataStart, string packageId, int groupCount)
        {
            _data = data;
            _fileTableStart = fileTableStart;
            _dataStart = dataStart;
            PackageId = packageId;
            GroupCount = groupCount;
        }

        /// <summary>从文件加载 .mcp 包。</summary>
        public static McpPackage Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            return Parse(data);
        }

        public static McpPackage Parse(byte[] data)
        {
            if (data.Length < HeaderSize)
                throw new InvalidDataException("文件太小，不是有效的 MCPK 包");

            if (!(data[0] == 'M' && data[1] == 'C' && data[2] == 'P' && data[3] == 'K'))
                throw new InvalidDataException("缺少 MCPK 魔数");

            uint groupTableStart = BitConverter.ToUInt32(data, 12);
            uint fileTableStart = BitConverter.ToUInt32(data, 16);
            uint dataStart = BitConverter.ToUInt32(data, 20);

            if (groupTableStart != HeaderSize)
                groupTableStart = HeaderSize;   // 兼容：实际就是 57

            // packageId：从 24 开始，直到 NUL
            int idEnd = 24;
            while (idEnd < data.Length && idEnd < 128 && data[idEnd] != 0)
                idEnd++;
            string packageId = Encoding.UTF8.GetString(data, 24, idEnd - 24);

            // 组表项数
            int groupCount = (int)((fileTableStart - groupTableStart) / 12);

            McpPackage pkg = new(data, fileTableStart, dataStart, packageId, groupCount);

            // 文件表
            int entryCount = (int)((dataStart - fileTableStart) / 16);
            for (int i = 0; i < entryCount; i++)
            {
                int pos = (int)fileTableStart + i * 16;
                if (pos + 16 > data.Length)
                    break;

                pkg.Entries.Add(new Entry
                {
                    File = BitConverter.ToUInt32(data, pos),
                    Offset = BitConverter.ToUInt32(data, pos + 4),
                    Size = BitConverter.ToUInt32(data, pos + 8),
                    OriginSize = BitConverter.ToUInt32(data, pos + 12),
                });
            }

            return pkg;
        }

        /// <summary>取出第 <paramref name="index"/> 个模块的原始（仍加密的）payload。</summary>
        public byte[] GetRaw(int index)
        {
            Entry e = Entries[index];
            long abs = (long)_dataStart + e.Offset;

            if (abs < 0 || abs + e.Size > _data.Length)
                throw new InvalidDataException($"模块 {index} 越界");

            byte[] raw = new byte[e.Size];
            Array.Copy(_data, abs, raw, 0, e.Size);
            return raw;
        }

        /// <summary>
        /// 解码单个模块，返回 pymarshal 之后的字节（仍是加密的 pyc，尚未做 pymarshal 解密）。
        /// </summary>
        public static byte[] DecodeModule(byte[] raw)
        {
            // 1. Rotor 解密
            RotorDecryptor rotor = new(6, DynamicKey);
            byte[] buf = (byte[])raw.Clone();
            rotor.DRegion(buf, true);

            // 2. zlib 解压
            byte[] inflated = Inflate(buf);

            // 3. 前 130 字节 XOR 0x9C
            int xorLen = Math.Min(130, inflated.Length);
            for (int i = 0; i < xorLen; i++)
                inflated[i] ^= 0x9C;

            // 4. 整体反转
            Array.Reverse(inflated);

            return inflated;
        }

        /// <summary>zlib 解压（跳过 2 字节 zlib 头）。</summary>
        private static byte[] Inflate(byte[] data)
        {
            // 先尝试标准 zlib
            try
            {
                using MemoryStream input = new(data);
                using ZLibStream z = new(input, CompressionMode.Decompress);
                using MemoryStream output = new();
                z.CopyTo(output);
                return output.ToArray();
            }
            catch
            {
                // 退回 raw deflate（跳过 2 字节头）
                using MemoryStream input = new(data, 2, data.Length - 2);
                using DeflateStream d = new(input, CompressionMode.Decompress);
                using MemoryStream output = new();
                d.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
