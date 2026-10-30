using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using PLDUOBAO.Network.NewVerify;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Security
{
    internal class ChaChaEngine : ChaCha7539Engine
    {
        public override string AlgorithmName => $"ChaCha{rounds}";
        public ChaChaEngine(int rounds, byte[] key, bool encryption = true)
        {
            base.rounds = rounds;
            Init(encryption, new ParametersWithIV(new KeyParameter(key), _Iv));
        }

        private static readonly byte[] _Iv = "163 NetEase\n"u8.ToArray();

        public void Encrypt(byte[] data) => this.ProcessBytes(data, 0, data.Length, data, 0);

        public void Decrypt(byte[] data) => this.ProcessBytes(data, 0, data.Length, data, 0);

        public void Encrypt(byte[] data, int len) => this.ProcessBytes(data, 0, len, data, 0);
                                                     
        public void Decrypt(byte[] data, int len) => this.ProcessBytes(data, 0, len, data, 0);
    }
}
