using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Mcp
{
    /// <summary>
    /// 网易 pymarshal 的解密器（PycRepairer 的 C# 移植）。
    ///
    /// 移植自 github.com/unmcpk/pymarshal：
    ///   convert.go / netease_types.go / netease_opcode.go / opcode.go
    ///
    /// 把网易魔改的 marshal 流还原成标准 Python 2.7 pyc。
    /// </summary>
    internal sealed class PycRepairer
    {
        // ---- 标准 marshal 类型 ----
        private const byte TYPE_NULL = (byte)'0';
        private const byte TYPE_NONE = (byte)'N';
        private const byte TYPE_STOPITER = (byte)'S';
        private const byte TYPE_ELLIPSIS = (byte)'.';
        private const byte TYPE_FALSE = (byte)'F';
        private const byte TYPE_TRUE = (byte)'T';
        private const byte TYPE_INT = (byte)'i';
        private const byte TYPE_INT64 = (byte)'I';
        private const byte TYPE_LONG = (byte)'l';
        private const byte TYPE_FLOAT = (byte)'f';
        private const byte TYPE_BINARY_FLOAT = (byte)'g';
        private const byte TYPE_COMPLEX = (byte)'x';
        private const byte TYPE_BINARY_COMPLEX = (byte)'y';
        private const byte TYPE_STRING = (byte)'s';
        private const byte TYPE_UNICODE = (byte)'u';
        private const byte TYPE_INTERNED = (byte)'t';
        private const byte TYPE_STRINGREF = (byte)'R';
        private const byte TYPE_TUPLE = (byte)'(';
        private const byte TYPE_LIST = (byte)'[';
        private const byte TYPE_SET = (byte)'<';
        private const byte TYPE_FROZENSET = (byte)'>';
        private const byte TYPE_DICT = (byte)'{';
        private const byte TYPE_CODE = (byte)'c';

        // ---- 网易私有的 marshal 类型 ----
        private const byte TYPE_NETEASE_CODE_0 = 0x61;
        private const byte TYPE_NETEASE_CODE_1 = 0x6f;
        private const byte TYPE_NETEASE_CODE_2 = 0x4d;

        private const byte TYPE_NETEASE_STRING_0 = 0x08;
        private const byte TYPE_NETEASE_INTERNED_0 = 0x0f;
        private const byte TYPE_NETEASE_UNICODE_0 = 0x0e;

        private const byte TYPE_NETEASE_STRING_1 = 0x17;
        private const byte TYPE_NETEASE_INTERNED_1 = 0x1a;
        private const byte TYPE_NETEASE_UNICODE_1 = 0x1d;

        private const byte TYPE_NETEASE_STRING_2 = 0x6d;
        private const byte TYPE_NETEASE_INTERNED_2 = 0x62;
        private const byte TYPE_NETEASE_UNICODE_2 = 0x31;

        public static readonly byte[] PycHeader = { 0x03, 0xF3, 0x0D, 0x0A, 0, 0, 0, 0 };

        /// <summary>StringVersion=2 使用的 RC4 密钥。</summary>
        private static readonly byte[] Rc4KeyV2 =
            { 0x8d, 0x06, 0xe8, 0xc8, 0xb7, 0xd7, 0xb7, 0x28, 0x46, 0x51, 0xae, 0x04 };

        /// <summary>StringVersion=1 使用的 RC4 密钥。</summary>
        private static readonly byte[] Rc4KeyV1 = { 0xA7, 0x0D, 0x37, 0x7A };

        private readonly byte[] _input;
        private int _pos;
        private readonly List<byte[]> _strings = new();

        private PycRepairer(byte[] input)
        {
            _input = input;
            _pos = 0;
        }

        /// <summary>把网易 marshal 流还原成标准 pyc 字节（含 PycHeader）。</summary>
        public static byte[] Repair(byte[] neteaseMarshal)
        {
            PycRepairer r = new(neteaseMarshal);
            using MemoryStream output = new();
            output.Write(PycHeader, 0, PycHeader.Length);
            r.RepairObject(output);
            return output.ToArray();
        }

        // ---------------- 读取辅助 ----------------

        private byte ReadByte()
        {
            if (_pos >= _input.Length)
                throw new EndOfStreamException("marshal 数据提前结束");
            return _input[_pos++];
        }

        private bool TryReadByte(out byte value)
        {
            if (_pos >= _input.Length) { value = 0; return false; }
            value = _input[_pos++];
            return true;
        }

        private int ReadInt32()
        {
            if (_pos + 4 > _input.Length)
                throw new EndOfStreamException("int32 越界");
            int v = BitConverter.ToInt32(_input, _pos);
            _pos += 4;
            return v;
        }

        private byte[] ReadBytes(int count)
        {
            if (count < 0 || _pos + count > _input.Length)
                throw new EndOfStreamException("字节读取越界");
            byte[] buf = new byte[count];
            Array.Copy(_input, _pos, buf, 0, count);
            _pos += count;
            return buf;
        }

        private static void WriteInt32(Stream s, int v)
        {
            byte[] b = BitConverter.GetBytes(v);
            s.Write(b, 0, 4);
        }

        // ---------------- 核心递归 ----------------

        private enum RepairResult { Ok, Null }

        private RepairResult RepairObject(Stream output)
        {
            byte tag;

            // 特殊标记优先处理，处理完直接返回
            switch (tag = ReadByte())
            {
                case TYPE_NETEASE_STRING_0:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseString0(output, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_UNICODE_0:
                    output.WriteByte(TYPE_UNICODE);
                    RepairNetEaseString0(output, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_INTERNED_0:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseString0(output, false);
                    return RepairResult.Ok;

                case TYPE_NETEASE_STRING_1:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseStringRc4(output, Rc4KeyV1, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_UNICODE_1:
                    output.WriteByte(TYPE_UNICODE);
                    RepairNetEaseStringRc4(output, Rc4KeyV1, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_INTERNED_1:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseStringRc4(output, Rc4KeyV1, false);
                    return RepairResult.Ok;

                case TYPE_NETEASE_STRING_2:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseStringRc4(output, Rc4KeyV2, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_UNICODE_2:
                    output.WriteByte(TYPE_UNICODE);
                    RepairNetEaseStringRc4(output, Rc4KeyV2, true);
                    return RepairResult.Ok;

                case TYPE_NETEASE_INTERNED_2:
                    output.WriteByte(TYPE_STRING);
                    RepairNetEaseStringRc4(output, Rc4KeyV2, false);
                    return RepairResult.Ok;

                case TYPE_NETEASE_CODE_0:
                    output.WriteByte(TYPE_CODE);
                    RepairNetEaseCode(output);
                    return RepairResult.Ok;

                case TYPE_NETEASE_CODE_1:
                    output.WriteByte(TYPE_CODE);
                    RepairNetEaseCode(output);
                    return RepairResult.Ok;

                case TYPE_NETEASE_CODE_2:
                    output.WriteByte(TYPE_CODE);
                    RepairNetEaseCode(output);
                    return RepairResult.Ok;

                case TYPE_INTERNED:
                    output.WriteByte(TYPE_STRING);
                    {
                        int n = ReadInt32();
                        WriteInt32(output, n);
                        byte[] data = ReadBytes(n);
                        output.Write(data, 0, data.Length);
                        _strings.Add(data);
                    }
                    return RepairResult.Ok;

                case TYPE_STRINGREF:
                    output.WriteByte(TYPE_STRING);
                    {
                        int n = ReadInt32();
                        byte[] str = _strings[n];
                        WriteInt32(output, str.Length);
                        output.Write(str, 0, str.Length);
                    }
                    return RepairResult.Ok;
            }

            // 标准类型：先写 tag 再写内容
            output.WriteByte(tag);

            switch (tag)
            {
                case TYPE_NULL:
                    return RepairResult.Null;

                case TYPE_NONE:
                case TYPE_STOPITER:
                case TYPE_ELLIPSIS:
                case TYPE_FALSE:
                case TYPE_TRUE:
                    return RepairResult.Ok;

                case TYPE_INT:
                    output.Write(ReadBytes(4), 0, 4);
                    return RepairResult.Ok;

                case TYPE_INT64:
                    output.Write(ReadBytes(8), 0, 8);
                    return RepairResult.Ok;

                case TYPE_LONG:
                    CopyPyLong(output);
                    return RepairResult.Ok;

                case TYPE_FLOAT:
                    CopyFloat(output);
                    return RepairResult.Ok;

                case TYPE_BINARY_FLOAT:
                    output.Write(ReadBytes(8), 0, 8);
                    return RepairResult.Ok;

                case TYPE_COMPLEX:
                    CopyFloat(output);
                    CopyFloat(output);
                    return RepairResult.Ok;

                case TYPE_BINARY_COMPLEX:
                    output.Write(ReadBytes(16), 0, 16);
                    return RepairResult.Ok;

                case TYPE_UNICODE:
                case TYPE_STRING:
                    {
                        int n = ReadInt32();
                        WriteInt32(output, n);
                        byte[] data = ReadBytes(n);
                        output.Write(data, 0, data.Length);
                    }
                    return RepairResult.Ok;

                case TYPE_TUPLE:
                case TYPE_SET:
                case TYPE_FROZENSET:
                case TYPE_LIST:
                    CopyList(output);
                    return RepairResult.Ok;

                case TYPE_DICT:
                    CopyDict(output);
                    return RepairResult.Ok;

                case TYPE_CODE:
                    output.Write(ReadBytes(16), 0, 16);   // argcount/locals/stacksize/flags
                    RepairOpcode(output, 0);
                    for (int i = 0; i < 7; i++)
                        RepairObject(output);
                    output.Write(ReadBytes(4), 0, 4);     // firstlineno
                    RepairObject(output);                  // lnotab
                    return RepairResult.Ok;

                default:
                    throw new InvalidDataException($"未知 marshal 类型 0x{tag:X2}");
            }
        }

        // ---------------- 各类处理 ----------------

        private void RepairNetEaseString0(Stream output, bool write)
        {
            int n = ReadInt32();
            WriteInt32(output, n);
            byte[] data = ReadBytes(n);

            for (int i = 0; i < data.Length; i++)
                data[i] ^= 0x8D;

            output.Write(data, 0, data.Length);
            if (!write) _strings.Add(data);
        }

        private void RepairNetEaseStringRc4(Stream output, byte[] key, bool write)
        {
            int n = ReadInt32();
            WriteInt32(output, n);
            byte[] data = ReadBytes(n);

            byte[] decrypted = Rc4(key, data);
            output.Write(decrypted, 0, decrypted.Length);
            if (!write) _strings.Add(decrypted);
        }

        private void RepairOpcode(Stream output, uint id)
        {
            output.WriteByte(ReadByte());      // 占位标记
            int n = ReadInt32();
            WriteInt32(output, n);
            byte[] data = ReadBytes(n);

            if (NeteaseOpcodes.TryGetValue(id, out Dictionary<byte, string>? netMap))
            {
                for (int i = 0; i < data.Length; i++)
                {
                    if (netMap.TryGetValue(data[i], out string? name)
                        && PythonOpcodes.TryGetValue(name, out byte pyOp))
                    {
                        data[i] = pyOp;
                    }
                }
            }

            output.Write(data, 0, data.Length);
        }

        /// <summary>
        /// 网易 code 对象的字段顺序被重排过（CodeVersion=2 的顺序）。
        /// 必须按写入顺序读回，再按标准 Python 2.7 顺序写出。
        ///
        /// CodeVersion=2 的写入顺序（见 encrypt.go）：
        ///   argcount, lnotab, cellvars, firstlineno, varnames, consts, name,
        ///   stacksize, freevars, names, codeBuf, flags, filename, locals, opcodeType
        /// </summary>
        private void RepairNetEaseCode(Stream output)
        {
            byte[] argcount = ReadBytes(4);

            using MemoryStream lnotab = new(); RepairObject(lnotab);
            using MemoryStream cellvars = new(); RepairObject(cellvars);

            byte[] firstlineno = ReadBytes(4);

            using MemoryStream varnames = new(); RepairObject(varnames);
            using MemoryStream consts = new(); RepairObject(consts);
            using MemoryStream name = new(); RepairObject(name);

            byte[] stacksize = ReadBytes(4);

            using MemoryStream freevars = new(); RepairObject(freevars);
            using MemoryStream names = new(); RepairObject(names);

            using MemoryStream codeBuf = new(); RepairOpcode(codeBuf, 1);

            byte[] flags = ReadBytes(4);

            using MemoryStream filename = new(); RepairObject(filename);

            byte[] locals = ReadBytes(4);

            // 末尾是 opcodeType（4 字节），读掉即可
            ReadBytes(4);

            // ---- 按标准 Python 2.7 顺序输出 ----
            output.Write(argcount, 0, argcount.Length);
            output.Write(locals, 0, locals.Length);
            output.Write(stacksize, 0, stacksize.Length);
            output.Write(flags, 0, flags.Length);
            codeBuf.Position = 0; codeBuf.CopyTo(output);
            consts.Position = 0; consts.CopyTo(output);
            names.Position = 0; names.CopyTo(output);
            varnames.Position = 0; varnames.CopyTo(output);
            freevars.Position = 0; freevars.CopyTo(output);
            cellvars.Position = 0; cellvars.CopyTo(output);
            filename.Position = 0; filename.CopyTo(output);
            name.Position = 0; name.CopyTo(output);
            output.Write(firstlineno, 0, firstlineno.Length);
            lnotab.Position = 0; lnotab.CopyTo(output);
        }

        private void CopyFloat(Stream output)
        {
            byte size = ReadByte();
            output.WriteByte(size);
            output.Write(ReadBytes(size), 0, size);
        }

        private void CopyList(Stream output)
        {
            int n = ReadInt32();
            WriteInt32(output, n);
            for (int i = 0; i < n; i++)
                RepairObject(output);
        }

        private void CopyDict(Stream output)
        {
            while (true)
            {
                if (RepairObject(output) == RepairResult.Null)
                    break;

                if (RepairObject(output) == RepairResult.Null)
                    break;
            }
        }

        private void CopyPyLong(Stream output)
        {
            int n = ReadInt32();
            WriteInt32(output, n);
            if (n == 0)
                return;

            const int Ratio = 15;
            const int Base = 32767;

            int abs = Math.Abs(n);
            int size = 1 + (abs - 1) / Ratio;
            int topShorts = 1 + (abs - 1) % Ratio;
            int totalShorts = Math.Abs(n) > 0 ? (size - 1) * Ratio + topShorts : 0;

            byte[] buf = ReadBytes(totalShorts * 2);
            output.Write(buf, 0, buf.Length);
        }

        // ---------------- RC4 ----------------

        private static byte[] Rc4(byte[] key, byte[] data)
        {
            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++) s[i] = (byte)i;

            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + s[i] + key[i % key.Length]) & 0xFF;
                (s[i], s[j]) = (s[j], s[i]);
            }

            byte[] output = new byte[data.Length];
            int a = 0, b = 0;
            for (int i = 0; i < data.Length; i++)
            {
                a = (a + 1) & 0xFF;
                b = (b + s[a]) & 0xFF;
                (s[a], s[b]) = (s[b], s[a]);
                byte k = s[(s[a] + s[b]) & 0xFF];
                output[i] = (byte)(data[i] ^ k);
            }
            return output;
        }

        // ---------------- opcode 表 ----------------

        private static readonly Dictionary<string, byte> PythonOpcodes = new()
        {
            ["POP_TOP"] = 1, ["ROT_TWO"] = 2, ["ROT_THREE"] = 3, ["DUP_TOP"] = 4,
            ["ROT_FOUR"] = 5, ["NOP"] = 9, ["UNARY_POSITIVE"] = 10, ["UNARY_NEGATIVE"] = 11,
            ["UNARY_NOT"] = 12, ["UNARY_CONVERT"] = 13, ["UNARY_INVERT"] = 15,
            ["BINARY_POWER"] = 19, ["BINARY_MULTIPLY"] = 20, ["BINARY_DIVIDE"] = 21,
            ["BINARY_MODULO"] = 22, ["BINARY_ADD"] = 23, ["BINARY_SUBTRACT"] = 24,
            ["BINARY_SUBSCR"] = 25, ["BINARY_FLOOR_DIVIDE"] = 26, ["BINARY_TRUE_DIVIDE"] = 27,
            ["INPLACE_FLOOR_DIVIDE"] = 28, ["INPLACE_TRUE_DIVIDE"] = 29,
            ["SLICE_0"] = 30, ["SLICE_1"] = 31, ["SLICE_2"] = 32, ["SLICE_3"] = 33,
            ["STORE_SLICE_0"] = 40, ["STORE_SLICE_1"] = 41, ["STORE_SLICE_2"] = 42,
            ["STORE_SLICE_3"] = 43, ["DELETE_SLICE_0"] = 50, ["DELETE_SLICE_1"] = 51,
            ["DELETE_SLICE_2"] = 52, ["DELETE_SLICE_3"] = 53, ["STORE_MAP"] = 54,
            ["INPLACE_ADD"] = 55, ["INPLACE_SUBTRACT"] = 56, ["INPLACE_MULTIPLY"] = 57,
            ["INPLACE_DIVIDE"] = 58, ["INPLACE_MODULO"] = 59, ["STORE_SUBSCR"] = 60,
            ["DELETE_SUBSCR"] = 61, ["BINARY_LSHIFT"] = 62, ["BINARY_RSHIFT"] = 63,
            ["BINARY_AND"] = 64, ["BINARY_XOR"] = 65, ["BINARY_OR"] = 66,
            ["INPLACE_POWER"] = 67, ["GET_ITER"] = 68, ["PRINT_EXPR"] = 70,
            ["PRINT_ITEM"] = 71, ["PRINT_NEWLINE"] = 72, ["PRINT_ITEM_TO"] = 73,
            ["PRINT_NEWLINE_TO"] = 74, ["INPLACE_LSHIFT"] = 75, ["INPLACE_RSHIFT"] = 76,
            ["INPLACE_AND"] = 77, ["INPLACE_XOR"] = 78, ["INPLACE_OR"] = 79,
            ["BREAK_LOOP"] = 80, ["WITH_CLEANUP"] = 81, ["LOAD_LOCALS"] = 82,
            ["RETURN_VALUE"] = 83, ["IMPORT_STAR"] = 84, ["EXEC_STMT"] = 85,
            ["YIELD_VALUE"] = 86, ["POP_BLOCK"] = 87, ["END_FINALLY"] = 88,
            ["BUILD_CLASS"] = 89, ["STORE_NAME"] = 90, ["DELETE_NAME"] = 91,
            ["UNPACK_SEQUENCE"] = 92, ["FOR_ITER"] = 93, ["LIST_APPEND"] = 94,
            ["STORE_ATTR"] = 95, ["DELETE_ATTR"] = 96, ["STORE_GLOBAL"] = 97,
            ["DELETE_GLOBAL"] = 98, ["DUP_TOPX"] = 99, ["LOAD_CONST"] = 100,
            ["LOAD_NAME"] = 101, ["BUILD_TUPLE"] = 102, ["BUILD_LIST"] = 103,
            ["BUILD_SET"] = 104, ["BUILD_MAP"] = 105, ["LOAD_ATTR"] = 106,
            ["COMPARE_OP"] = 107, ["IMPORT_NAME"] = 108, ["IMPORT_FROM"] = 109,
            ["JUMP_FORWARD"] = 110, ["JUMP_IF_FALSE_OR_POP"] = 111,
            ["JUMP_IF_TRUE_OR_POP"] = 112, ["JUMP_ABSOLUTE"] = 113,
            ["POP_JUMP_IF_FALSE"] = 114, ["POP_JUMP_IF_TRUE"] = 115,
            ["LOAD_GLOBAL"] = 116, ["CONTINUE_LOOP"] = 119, ["SETUP_LOOP"] = 120,
            ["SETUP_EXCEPT"] = 121, ["SETUP_FINALLY"] = 122, ["LOAD_FAST"] = 124,
            ["STORE_FAST"] = 125, ["DELETE_FAST"] = 126, ["RAISE_VARARGS"] = 130,
            ["CALL_FUNCTION"] = 131, ["MAKE_FUNCTION"] = 132, ["BUILD_SLICE"] = 133,
            ["MAKE_CLOSURE"] = 134, ["LOAD_CLOSURE"] = 135, ["LOAD_DEREF"] = 136,
            ["STORE_DEREF"] = 137, ["CALL_FUNCTION_VAR"] = 140, ["CALL_FUNCTION_KW"] = 141,
            ["CALL_FUNCTION_VAR_KW"] = 142, ["SETUP_WITH"] = 143, ["EXTENDED_ARG"] = 145,
            ["SET_ADD"] = 146, ["MAP_ADD"] = 147,
        };

        /// <summary>
        /// 网易 opcode 表。id=0 对应 after-2.7 的那张表（本项目实际使用）。
        /// </summary>
        private static readonly Dictionary<uint, Dictionary<byte, string>> NeteaseOpcodes = new();

        static PycRepairer()
        {
            // after 2.7（id=0），完整表来自 netease_opcode.go
            NeteaseOpcodes[0] = new Dictionary<byte, string>
            {
                [0x6e] = "DUP_TOPX", [0x5c] = "BINARY_AND", [0x47] = "INPLACE_TRUE_DIVIDE",
                [0x87] = "DELETE_NAME", [0x0a] = "INPLACE_MODULO", [0x34] = "ROT_THREE",
                [0x40] = "DELETE_SLICE_2", [0x2f] = "PRINT_ITEM_TO", [0x66] = "EXTENDED_ARG",
                [0x1c] = "SLICE_1", [0xd1] = "SETUP_EXCEPT", [0x1a] = "WITH_CLEANUP",
                [0x18] = "INPLACE_MULTIPLY", [0xd4] = "STORE_ATTR", [0x1e] = "SLICE_3",
                [0xd0] = "SETUP_LOOP", [0x53] = "POP_TOP", [0x88] = "LOAD_GLOBAL",
                [0x6b] = "FOR_ITER", [0x36] = "BINARY_SUBTRACT", [0xd5] = "JUMP_IF_TRUE_OR_POP",
                [0xbd] = "SET_ADD", [0x42] = "UNARY_CONVERT", [0x14] = "STORE_MAP",
                [0xb6] = "RAISE_VARARGS", [0x03] = "UNARY_INVERT", [0x1f] = "BINARY_RSHIFT",
                [0x0f] = "GET_ITER", [0xfa] = "IMPORT_NAME", [0x12] = "YIELD_VALUE",
                [0x07] = "STORE_SLICE_3", [0x08] = "BINARY_SUBSCR", [0x29] = "INPLACE_AND",
                [0x79] = "JUMP_FORWARD", [0xa1] = "BUILD_MAP", [0x17] = "PRINT_ITEM",
                [0x8c] = "LOAD_FAST", [0x52] = "IMPORT_STAR", [0x04] = "STORE_SLICE_0",
                [0xda] = "POP_JUMP_IF_FALSE", [0x51] = "RETURN_VALUE", [0xc3] = "JUMP_IF_FALSE_OR_POP",
                [0x0b] = "BINARY_POWER", [0x10] = "INPLACE_SUBTRACT", [0x3b] = "BINARY_LSHIFT",
                [0xd8] = "LIST_APPEND", [0xdf] = "MAP_ADD", [0xfe] = "UNPACK_SEQUENCE",
                [0x44] = "INPLACE_OR", [0x09] = "PRINT_NEWLINE", [0x6f] = "BUILD_SET",
                [0x01] = "ROT_FOUR", [0x0d] = "INPLACE_LSHIFT", [0xe9] = "CALL_FUNCTION_VAR",
                [0xd6] = "LOAD_CLOSURE", [0x2e] = "BREAK_LOOP", [0x2c] = "UNARY_POSITIVE",
                [0x28] = "END_FINALLY", [0x89] = "LOAD_ATTR", [0x5a] = "NOP",
                [0xea] = "CALL_FUNCTION_KW", [0x23] = "STORE_SUBSCR", [0x3e] = "DELETE_SLICE_0",
                [0x84] = "LOAD_NAME", [0x35] = "LOAD_LOCALS", [0x9f] = "STORE_FAST",
                [0xc2] = "SETUP_WITH", [0x0e] = "BINARY_TRUE_DIVIDE", [0x56] = "UNARY_NOT",
                [0x4d] = "INPLACE_ADD", [0xfc] = "BUILD_SLICE", [0x58] = "PRINT_NEWLINE_TO",
                [0x60] = "STORE_DEREF", [0x11] = "POP_BLOCK", [0xc9] = "STORE_NAME",
                [0xa7] = "CONTINUE_LOOP", [0x46] = "UNARY_NEGATIVE", [0x1d] = "SLICE_2",
                [0xeb] = "CALL_FUNCTION_VAR_KW", [0x13] = "INPLACE_FLOOR_DIVIDE",
                [0xe1] = "DELETE_ATTR", [0x50] = "ROT_TWO", [0x33] = "INPLACE_POWER",
                [0x31] = "PRINT_EXPR", [0xa3] = "IMPORT_FROM", [0x85] = "LOAD_DEREF",
                [0x4e] = "BINARY_OR", [0x4b] = "BINARY_MODULO", [0x1b] = "SLICE_0",
                [0xf3] = "POP_JUMP_IF_TRUE", [0xde] = "LOAD_CONST", [0x3f] = "DELETE_SLICE_1",
                [0x06] = "STORE_SLICE_2", [0x48] = "BINARY_XOR", [0x05] = "STORE_SLICE_1",
                [0xf7] = "MAKE_FUNCTION", [0xe5] = "BUILD_TUPLE", [0x15] = "BINARY_MULTIPLY",
                [0xf0] = "DELETE_GLOBAL", [0xa2] = "DELETE_FAST", [0xaf] = "BUILD_LIST",
                [0x74] = "SETUP_FINALLY", [0x59] = "DELETE_SUBSCR", [0x43] = "BUILD_CLASS",
                [0x2a] = "BINARY_DIVIDE", [0x7b] = "JUMP_ABSOLUTE", [0x4f] = "EXEC_STMT",
                [0xa6] = "MAKE_CLOSURE", [0x90] = "CALL_FUNCTION", [0x2b] = "DUP_TOP",
                [0x86] = "STORE_GLOBAL", [0x27] = "INPLACE_DIVIDE", [0x02] = "INPLACE_RSHIFT",
                [0x41] = "DELETE_SLICE_3", [0x3c] = "INPLACE_XOR", [0x57] = "BINARY_FLOOR_DIVIDE",
                [0x49] = "BINARY_ADD", [0xe4] = "COMPARE_OP",
            };
        }
    }
}
