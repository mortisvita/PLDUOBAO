using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Codexus.Server.G79
{
    internal class PESign
    {
        private static readonly uint[] signShiftTable = {
        0xD0A0601,
        0xE090502,
        0x30B0704,
        0x50B0803,
        0xE0B0701
    };

        private static readonly uint[] signConstantTable = {
        0xF61E2562,
        0xC040B340,
        0x265E5A51,
        0xE9B6C7AA,
        0xA4BEEA44,
        0x4BDECFA9,
        0xF6BB4B60,
        0xBEBFBC70,
        0x655B59C3,
        0x8F0CCC92,
        0xFFEFF47D,
        0x85845DD1,
        0x289B7EC6,
        0xEAA127FA,
        0xD4EF3085,
        0x04881D05,
        0x21E1CDE6,
        0xC33707D6,
        0xF4D50D87,
        0x455A14ED,
        0xF61E2562,
        0xC040B340,
        0x265E5A51,
        0xE9B6C7AA,
        0xA4BEEA44,
        0x4BDECFA9,
        0xF6BB4B60,
        0xBEBFBC70,
        0x655B59C3,
        0x8F0CCC92,
        0xFFEFF47D,
        0x85845DD1,
        0x289B7EC6,
        0xEAA127FA,
        0xD4EF3085,
        0x04881D05,
        0x21E1CDE6,
        0xC33707D6,
        0xF4D50D87,
        0x455A14ED
    };

        public static byte[]? PESignCount(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }
            int offset = 4;
            int rounds = 9;
            return ComputeSignature(message, offset, rounds);
        }

        public static byte[]? PESignCount(string message, int offset, int rounds)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }
            return ComputeSignature(message, offset, rounds);
        }
        private static byte[] ComputeSignature(string message, int offset, int rounds)
        {
            // 填充字符串到4的倍数
            StringBuilder messageBuilder = new StringBuilder(message);
            while (messageBuilder.Length % 4 != 0)
            {
                messageBuilder.Append('0');
            }
            message = messageBuilder.ToString();

            // 将字符串分组为4字节整数
            List<uint> group = new List<uint>();
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            for (int i = 0; i < bytes.Length; i += 4)
            {
                uint value = ((uint)(bytes[i] & 0xFF) << 24) |
                           ((uint)(bytes[i + 1] & 0xFF) << 16) |
                           ((uint)(bytes[i + 2] & 0xFF) << 8) |
                           (uint)(bytes[i + 3] & 0xFF);
                group.Add(value);
            }

            // 填充到64的倍数
            while (group.Count % 64 != 0)
            {
                group.Add(0xABCDE987);
            }

            uint shiftBase = signShiftTable[offset];
            uint shift0 = shiftBase & 0xFF;
            uint shift1 = (shiftBase >> 8) & 0xFF;
            uint shift2 = (shiftBase >> 16) & 0xFF;
            uint shift3 = (shiftBase >> 24) & 0xFF;

            int constBase = offset * 4;
            uint const0 = signConstantTable[constBase];
            uint const1 = signConstantTable[constBase + 1];
            uint const2 = signConstantTable[constBase + 2];
            uint const3 = signConstantTable[constBase + 3];

            uint hashA = 0x67452301;
            uint hashB = 0xEFCDAB89;
            uint hashC = 0x98BADCFE;
            uint hashD = 0x10325476;

            for (int round = 0; round < rounds; round++)
            {
                for (int i = 0; i < group.Count; i += 4)
                {
                    uint chunk = group[i];

                    // 第一步计算
                    uint step1 = chunk + const0 + (hashB & hashC | hashD & ~hashB) + hashA;
                    uint tempA = hashB + (step1 << (int)shift0);
                    hashA = tempA;

                    // 第二步计算
                    uint step2 = hashA + chunk + const1 + ((hashC & ~hashD) | (hashD & hashB));
                    uint tempB = hashB + (step2 << (int)shift1);
                    hashB = tempB;

                    // 第三步计算
                    uint step3 = hashA + chunk + const2 + (hashB ^ hashD ^ hashC);
                    uint tempC = hashB + (step3 << (int)shift2);
                    hashC = tempC;

                    // 第四步计算
                    uint step4 = hashA + chunk + const3 + (hashC ^ (hashB | hashD));
                    uint tempD = hashB + (step4 << (int)shift3);
                    hashD = tempD;
                }
            }

            // 转换为小端序字节数组
            byte[] hashBytes = new byte[16];
            Array.Copy(ToLittleEndian((int)hashA), 0, hashBytes, 0, 4);
            Array.Copy(ToLittleEndian((int)hashB), 0, hashBytes, 4, 4);
            Array.Copy(ToLittleEndian((int)hashC), 0, hashBytes, 8, 4);
            Array.Copy(ToLittleEndian((int)hashD), 0, hashBytes, 12, 4);

            return hashBytes;
        }

        private static byte[] ToLittleEndian(int value)
        {
            return new byte[]
            {
            (byte)value,
            (byte)(value >> 8),
            (byte)(value >> 16),
            (byte)(value >> 24)
            };
        }

        public static byte[] Sign(string data, int offset, int vector)
        {
            return ComputeSignature(data, offset, vector);
        }
    }
}
