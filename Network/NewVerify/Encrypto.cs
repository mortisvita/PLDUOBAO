using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;


namespace PLDUOBAO.Network.NewVerify
{
    internal class Encrypto
    {
        static readonly string key = "Samalaimatemasuperhumansowhaidbai8888888";

        public static string Enkureipito(string a)
        {
            return GetMD5(Encrypt(a));
        }

        public static string Encrypt(string data)
        {
            return data.Encrypt(key);
        }

        public static string Decrypt(string data) 
        {
            return data.Decrypt(key); 
        }
        public static string Encrypt_B64(string plainText)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

            byte[] encryptedBytes = new byte[plainTextBytes.Length];

            for (int i = 0; i < plainTextBytes.Length; i++)
            {
                encryptedBytes[i] = (byte)(plainTextBytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            string encryptedText = Convert.ToBase64String(encryptedBytes);
            return encryptedText;
        }

        public static string Decrypt_B64(string encryptedText)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] encryptedBytes = Convert.FromBase64String(encryptedText);

            byte[] decryptedBytes = new byte[encryptedBytes.Length];

            for (int i = 0; i < encryptedBytes.Length; i++)
            {
                decryptedBytes[i] = (byte)(encryptedBytes[i] ^ keyBytes[i % keyBytes.Length]);
            }

            string decryptedText = Encoding.UTF8.GetString(decryptedBytes);
            return decryptedText;
        }
        [SupportedOSPlatform("windows")]
        public static string GetHWID()
        {
            string location = @"SOFTWARE\Microsoft\Cryptography";
            string name = "MachineGuid";

            using (RegistryKey localMachineX64View =
                RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                    using (RegistryKey rk = localMachineX64View.OpenSubKey(location))
                    {
                        if (rk == null)
                            throw new KeyNotFoundException(
                                string.Format("Key Not Found: {0}", location));

                        object machineGuid = rk.GetValue(name);
                        if (machineGuid == null)
                            throw new IndexOutOfRangeException(
                                string.Format("Index Not Found: {0}", name));

                        return machineGuid.ToString();
                    }
            }
        }

        public static string GetMD5(string input)
        {
            // 将输入字符串转换为字节数组
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            // 创建 MD5 实例
            using (MD5 md5 = MD5.Create())
            {
                // 执行 MD5 计算
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                // 将字节数组转换为十六进制字符串
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }

                return sb.ToString();
            }
        }
    }
}
