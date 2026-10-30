using System;

namespace PLDUOBAO.Util
{
    internal static class CJKPathGuard
    {
        public const string RejectMessage = "路径包含中文，请移动至无中文路径后重试。";

        public static bool ContainsCJK(string? value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (IsCJKChar(c)) return true;

                if (char.IsHighSurrogate(c) && i + 1 < value.Length)
                {
                    int cp = char.ConvertToUtf32(c, value[i + 1]);
                    if (IsCJKExtCodePoint(cp)) return true;
                    i++;
                }
            }
            return false;
        }

        public static bool IsCJKChar(char c)
        {
            if (c >= '\u4E00' && c <= '\u9FFF') return true;
            if (c >= '\u3400' && c <= '\u4DBF') return true;
            if (c >= '\uFF00' && c <= '\uFFEF') return true;
            if (c >= '\u3000' && c <= '\u303F') return true;
            if (c >= '\u3040' && c <= '\u30FF') return true;
            if (c >= '\uAC00' && c <= '\uD7AF') return true;
            if (c >= '\u3130' && c <= '\u318F') return true;
            return false;
        }

        public static bool IsCJKExtCodePoint(int cp)
        {
            return cp >= 0x20000 && cp <= 0x2FA1F;
        }
    }
}