using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Util
{
    internal class StringGenerator
    {
        private static readonly Random Random = new Random();

        public static string GenerateHexString(int length)
        {
            byte[] array = new byte[length];
            Random.NextBytes(array);
            return BitConverter.ToString(array);
        }

        public static string GenerateRandomString(int length, bool includeNumbers = true, bool includeUppercase = true, bool includeLowercase = true)
        {
            if (length <= 0)
            {
                throw new ArgumentException("Length must be greater than 0", "length");
            }
            if (!includeNumbers && !includeUppercase && !includeLowercase)
            {
                throw new ArgumentException("Must include at least one character type", "includeNumbers, includeUppercase, includeLowercase");
            }
            StringBuilder stringBuilder = new StringBuilder();
            if (includeNumbers)
            {
                stringBuilder.Append("0123456789");
            }
            if (includeUppercase)
            {
                stringBuilder.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            }
            if (includeLowercase)
            {
                stringBuilder.Append("abcdefghijklmnopqrstuvwxyz");
            }
            int length2 = stringBuilder.Length;
            StringBuilder stringBuilder2 = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                stringBuilder2.Append(stringBuilder[Random.Next(length2)]);
            }
            return stringBuilder2.ToString();
        }

        public static string GenerateRandomMacAddress(string separator = ":", bool uppercase = true)
        {
            byte[] macBytes = new byte[6];
            Random.NextBytes(macBytes);

            // 设置为本地管理的 MAC 地址（清除第1位，设置第2位）
            macBytes[0] = (byte)((macBytes[0] & 0xFE) | 0x02);

            string format = uppercase ? "X2" : "x2";
            string[] hexParts = macBytes.Select(b => b.ToString(format)).ToArray();
            return string.Join(separator, hexParts);
        }
    }
}
