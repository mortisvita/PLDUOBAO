using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mark;
using Noya.LocalServer.Common.Extensions;

namespace PLDUOBAO.Util
{
    internal static class TokenUtil //Thank you codexus
    {
        public static Dictionary<string, string> ComputeHttpRequestToken(string requestPath, string sendBody, string userId, string userToken, bool isAuth = false)
        {
            return ComputeHttpRequestToken(requestPath, Encoding.UTF8.GetBytes(sendBody), userId, userToken,isAuth);
        }

        private static Dictionary<string, string> ComputeHttpRequestToken(string requestPath, byte[] sendBody, string userId, string userToken,bool isAuth)
        {
            requestPath = (requestPath.StartsWith('/') ? requestPath : ("/" + requestPath));
            using MemoryStream memoryStream = new MemoryStream();
            memoryStream.Write(Encoding.UTF8.GetBytes(userToken.EncodeMd5().ToLowerInvariant()));
            memoryStream.Write(sendBody);
            memoryStream.Write(Encoding.UTF8.GetBytes("0eGsBkhl"));
            memoryStream.Write(Encoding.UTF8.GetBytes(requestPath));
            string text = memoryStream.ToArray().EncodeMd5().ToLowerInvariant();
            string text2 = HexToBinary(text);
            string text3 = text2;
            text2 = text3.Substring(6, text3.Length - 6) + text2.Substring(0, 6);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            ProcessBinaryBlock(text2, bytes);
            string value = (Convert.ToBase64String(bytes, 0, 12) + "1").Replace('+', 'm').Replace('/', 'o');
            if (isAuth)
            {
                value = Encoding.ASCII.GetBytes(x19Crypt.ComputeDynamicToken(requestPath, Encoding.UTF8.GetString(sendBody),userToken)).ToHex(toUpper: true);
            }
            return new Dictionary<string, string>
            {
                ["user-id"] = userId,
                ["user-token"] = value
            };
        }

        private static void ProcessBinaryBlock(string secretBin, byte[] httpToken)
        {
            for (int i = 0; i < secretBin.Length / 8; i++)
            {
                ReadOnlySpan<char> readOnlySpan = secretBin.AsSpan(i * 8, Math.Min(8, secretBin.Length - i * 8));
                byte b = 0;
                for (int j = 0; j < readOnlySpan.Length; j++)
                {
                    if (readOnlySpan[7 - j] == '1')
                    {
                        b |= (byte)(1 << j);
                    }
                }
                httpToken[i] ^= b;
            }
        }

        private static string HexToBinary(string hexString)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string item in hexString.Select((char hex) => Convert.ToString(hex, 2).PadLeft(8, '0')))
            {
                stringBuilder.Append(item);
            }
            return stringBuilder.ToString();
        }

        public static string EncodeMd5(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return Encoding.UTF8.GetBytes(input).EncodeMd5();
        }

        public static string EncodeMd5(this byte[] inputBytes)
        {
            return MD5.HashData(inputBytes).EncodeHex();
        }

        public static string EncodeHex(this byte[] input)
        {
            return Convert.ToHexString(input).Replace("-", "").ToLower();
        }

        public static byte[] DecodeHex(this string input)
        {
            return Convert.FromHexString(input);
        }

        public static byte[] EncodeAes(this string input, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            return cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
        }

        public static string EncodeBase64(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }
    }
}
