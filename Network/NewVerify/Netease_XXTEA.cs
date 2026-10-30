using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.NewVerify
{
    public static class NetEase_XXTEA
    {
        //加密函数
        public static string Encrypt(this string text, string key)
        {
            return NetEase_XXTEA.Encrypt(Encoding.UTF8.GetBytes(text.PadRight(32, '\0')).toLongData(), Encoding.UTF8.GetBytes(key.PadRight(32, '\0')).toLongData()).toString();
        }

        //解密函数
        public static string Decrypt(this string text, string key)
        {
            string result;
            if (string.IsNullOrWhiteSpace(text))
            {
                result = text;
            }
            else
            {
                byte[] array = NetEase_XXTEA.Decrypt(text.toLong(), Encoding.UTF8.GetBytes(key.PadRight(32, '\0')).toLongData()).toByteData();
                result = Encoding.UTF8.GetString(array, 0, array.Length);
            }
            return result;
        }

        private static long[] Encrypt(long[] textArray, long[] keyArray)
        {
            int num = textArray.Length;
            long[] result;
            if (num < 1)
            {
                result = textArray;
            }
            else
            {
                long num2 = textArray[textArray.Length - 1];
                long num3 = textArray[0];
                long num4 = 0L;
                long num5 = (long)(6 + 52 / num);
                for (; ; )
                {
                    long num6 = num5;
                    num5 = num6 - 1L;
                    if (num6 <= 0L)
                    {
                        break;
                    }
                    num4 += 2654435769L;
                    long num7 = num4 >> 2 & 3L;
                    long num8;
                    for (num8 = 0L; num8 < (long)(num - 1); num8 += 1L)
                    {
                        num3 = textArray[(int)(checked((IntPtr)(unchecked(num8 + 1L))))];
                        num2 = (textArray[(int)(checked((IntPtr)num8))] += ((num2 >> 5 ^ num3 << 2) + (num3 >> 3 ^ num2 << 4) ^ (num4 ^ num3) + (keyArray[(int)(checked((IntPtr)((num8 & 3L) ^ num7)))] ^ num2)));
                    }
                    num3 = textArray[0];
                    num2 = (textArray[num - 1] += ((num2 >> 5 ^ num3 << 2) + (num3 >> 3 ^ num2 << 4) ^ (num4 ^ num3) + (keyArray[(int)(checked((IntPtr)((num8 & 3L) ^ num7)))] ^ num2)));
                }
                result = textArray;
            }
            return result;
        }

        private static long[] Decrypt(long[] textArray, long[] keyArray)
        {
            int num = textArray.Length;
            long[] result;
            if (num < 1)
            {
                result = textArray;
            }
            else
            {
                long num2 = textArray[textArray.Length - 1];
                long num3 = textArray[0];
                long num4 = (long)(6 + 52 / num);
                for (long num5 = num4 * 2654435769L; num5 != 0L; num5 -= 2654435769L)
                {
                    long num6 = num5 >> 2 & 3L;
                    long num7;
                    for (num7 = (long)(num - 1); num7 > 0L; num7 -= 1L)
                    {
                        num2 = textArray[(int)(checked((IntPtr)(unchecked(num7 - 1L))))];
                        num3 = (textArray[(int)(checked((IntPtr)num7))] -= ((num2 >> 5 ^ num3 << 2) + (num3 >> 3 ^ num2 << 4) ^ (num5 ^ num3) + (keyArray[(int)(checked((IntPtr)((num7 & 3L) ^ num6)))] ^ num2)));
                    }
                    num2 = textArray[num - 1];
                    num3 = (textArray[0] -= ((num2 >> 5 ^ num3 << 2) + (num3 >> 3 ^ num2 << 4) ^ (num5 ^ num3) + (keyArray[(int)(checked((IntPtr)((num7 & 3L) ^ num6)))] ^ num2)));
                }
                result = textArray;
            }
            return result;
        }

        private static long[] toLongData(this byte[] byteData)
        {
            int num = ((byteData.Length % 8 == 0) ? 0 : 1) + byteData.Length / 8;
            long[] array = new long[num];
            for (int i = 0; i < num - 1; i++)
            {
                array[i] = BitConverter.ToInt64(byteData, i * 8);
            }
            byte[] array2 = new byte[8];
            Array.Copy(byteData, (num - 1) * 8, array2, 0, byteData.Length - (num - 1) * 8);
            array[num - 1] = BitConverter.ToInt64(array2, 0);
            return array;
        }

        private static byte[] toByteData(this long[] longData)
        {
            List<byte> list = new List<byte>(longData.Length * 8);
            for (int i = 0; i < longData.Length; i++)
            {
                list.AddRange(BitConverter.GetBytes(longData[i]));
            }
            while (list[list.Count - 1] == 0)
            {
                list.RemoveAt(list.Count - 1);
            }
            return list.ToArray();
        }

        private static string toString(this long[] longData)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < longData.Length; i++)
            {
                stringBuilder.Append(longData[i].ToString("x2").PadLeft(16, '0'));
            }
            return stringBuilder.ToString();
        }

        private static long[] toLong(this string stringData)
        {
            int num = stringData.Length / 16;
            long[] array = new long[num];
            for (int i = 0; i < num; i++)
            {
                array[i] = Convert.ToInt64(stringData.Substring(i * 16, 16), 16);
            }
            return array;
        }

    }
}
