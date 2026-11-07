using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Elysium.Core.Utils
{
    internal partial class LogSys
    {
        internal static List<string> ASCIIArts = new List<string>
        {
            "[PLDUOBAO] Debug console"
        };

#if DEBUG
        public static bool DebugMode = true;
#else
        public static bool DebugMode = false;
#endif

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AllocConsole();

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FreeConsole();

        #region 记录
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(object data)
        {
            if (!DebugMode) return;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] {data}");
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Warn(object data)
        {
            if (!DebugMode) return;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] {data}");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void Err(object data)
        {
            if (!DebugMode) return;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] {data}");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public static void ErrNW(string data, object extraInfo = null)
        {
            if (!DebugMode) return;
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"[{DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")}] {data}{(extraInfo == null ? "::Base" : extraInfo)}Not Work!!!");
            Console.ForegroundColor = ConsoleColor.Gray;
        }
        #endregion

        public static void Create(bool Notitle = false)
        {
#if DEBUG
            if (DebugMode)
            {
                AllocConsole();
                if (!Notitle)
                {
                    string randomString = ASCIIArts[0];
                    Console.WriteLine(randomString);
                    Warn("调试控制台已启动");
                }
            }
#endif
        }
    }
}
