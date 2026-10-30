using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Elysium.Core.Utils;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Security
{
    internal class RSAHelper
    {
        public static byte[] RSADecrypt(string xmlPrivateKey, byte[] decryptString)
        {
            try
            {
                RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
                rSACryptoServiceProvider.FromXmlString(xmlPrivateKey);
                return rSACryptoServiceProvider.Decrypt(decryptString, fOAEP: false);
            }
            catch (Exception ex)
            {
                LogSys.Err($"RSAHelper::Decrypt {ex}");
                throw;
            }
        }

        public static byte[] RSAEncrypt(string xmlPublicKey, byte[] EncryptString)
        {
            try
            {
                RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
                rSACryptoServiceProvider.FromXmlString(xmlPublicKey);
                return rSACryptoServiceProvider.Encrypt(EncryptString, fOAEP: false);
            }
            catch (Exception ex)
            {
                LogSys.Err($"RSAHelper::Encrypt {ex}");
                throw;
            }
        }
    }
}
