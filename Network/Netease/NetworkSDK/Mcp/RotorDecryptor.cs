using System;
using System.Collections.Generic;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Mcp
{
    // MCPK 的 Rotor 解密机。
    // 移植自 github.com/Yeah114/unmcpk 的 mcpk/rotor.go。
    // 用于解开 .mcp 包中每个模块的 Rotor 加密层（zlib 之前的那一层）。
    internal sealed class RotorDecryptor
    {
        private readonly short[] _key = new short[5];
        private readonly int[] _seed = new int[3];

        private const int Size = 256;
        private const int SizeMask = Size - 1;

        private readonly int _rotors;
        private readonly byte[] _dRotor;
        private readonly byte[] _positions;
        private readonly byte[] _advances;

        private bool _isInited;

        public RotorDecryptor(int numRotors, string key)
        {
            SetKey(key);

            _rotors = numRotors;
            _dRotor = new byte[numRotors * Size];
            _positions = new byte[numRotors];
            _advances = new byte[numRotors];
        }

        // ---------------- 密钥派生 ----------------

        private void SetKey(string key)
        {
            ulong k1 = 995, k2 = 576, k3 = 767, k4 = 671, k5 = 463;

            for (int i = 0; i < key.Length; i++)
            {
                ulong ki = (ulong)key[i] & 0xFF;

                k1 = ((k1 << 3) | (k1 >> 13)) + ki;
                k1 &= 65535;

                k2 = ((k2 << 3) | (k2 >> 13)) ^ ki;
                k2 &= 65535;

                k3 = ((k3 << 3) | (k3 >> 13)) - ki;
                k3 &= 65535;

                k4 = (ki - ((k4 << 3) | (k4 >> 13))) & 65535;

                k5 = ((k5 << 3) | (k5 >> 13)) ^ ((~ki) & 0xFFFF);
                k5 &= 65535;
            }

            _key[0] = (short)k1;
            _key[1] = (short)(k2 | 1);
            _key[2] = (short)k3;
            _key[3] = (short)k4;
            _key[4] = (short)k5;

            SetSeed();
        }

        private void SetSeed()
        {
            _seed[0] = _key[0];
            _seed[1] = _key[1];
            _seed[2] = _key[2];
            _isInited = false;
        }

        // ---------------- 伪随机 ----------------

        private double RRandom()
        {
            int x = _seed[0], y = _seed[1], z = _seed[2];

            x = 171 * (x % 177) - 2 * (x / 177);
            y = 172 * (y % 176) - 35 * (y / 176);
            z = 170 * (z % 178) - 63 * (z / 178);

            if (x < 0) x += 30269;
            if (y < 0) y += 30307;
            if (z < 0) z += 30323;

            _seed[0] = x;
            _seed[1] = y;
            _seed[2] = z;

            double term = (double)x / 30269.0 + (double)y / 30307.0 + (double)z / 30323.0;
            double val = term - Math.Floor(term);

            if (val >= 1.0)
                val = 0.0;

            return val;
        }

        private short RRand(short s)
        {
            return (short)((short)Math.Floor(RRandom() * s) % s);
        }

        // ---------------- 转子构建 ----------------

        private void MakeIdRotor(byte[] rtr, int offset)
        {
            for (int j = 0; j < Size; j++)
                rtr[offset + j] = (byte)j;
        }

        private void DRotors()
        {
            for (int i = 0; i < _rotors; i++)
                MakeIdRotor(_dRotor, i * Size);
        }

        private void Positions()
        {
            for (int i = 0; i < _rotors; i++)
                _positions[i] = 1;
        }

        private void Advances()
        {
            for (int i = 0; i < _rotors; i++)
                _advances[i] = 1;
        }

        // 生成 E 转子并同步生成其逆置换 D 转子。
        private void PermuteRotor(byte[] e, byte[] d, int eOff, int dOff)
        {
            int i = Size;
            MakeIdRotor(e, eOff);

            while (i >= 2)
            {
                int q = RRand((short)i);
                i--;
                byte j = e[eOff + q];
                e[eOff + q] = e[eOff + i];
                e[eOff + i] = j;
                d[dOff + j] = (byte)i;
            }

            d[dOff + e[eOff]] = 0;
        }

        // 初始化转子机。
        public void Init()
        {
            SetSeed();
            Positions();
            Advances();
            DRotors();

            byte[] eRotor = new byte[_rotors * Size];

            for (int i = 0; i < _rotors; i++)
            {
                _positions[i] = (byte)RRand(Size);
                _advances[i] = (byte)(1 + 2 * RRand(Size / 2));
                PermuteRotor(eRotor, _dRotor, i * Size, i * Size);
            }

            _isInited = true;
        }

        private void Advance()
        {
            for (int i = 0; i < _rotors; i++)
            {
                int temp = _positions[i] + _advances[i];
                _positions[i] = (byte)(temp & SizeMask);

                if (temp >= Size && i < _rotors - 1)
                    _positions[i + 1] = (byte)(1 + _positions[i + 1]);
            }
        }

        private byte DChar(byte c)
        {
            int tc = c;

            for (int i = _rotors - 1; i >= 0; i--)
                tc = (_positions[i] ^ _dRotor[i * Size + tc]) & SizeMask;

            Advance();
            return (byte)tc;
        }

        // 把整段数据就地解密。
        public void DRegion(byte[] data, bool doInit)
        {
            if (doInit || !_isInited)
                Init();

            for (int i = 0; i < data.Length; i++)
                data[i] = DChar(data[i]);
        }
    }
}
