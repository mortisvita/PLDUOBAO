using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Elysium.Core.Utils;
using PLDUOBAO.SDK;
using PLDUOBAO.Util;

namespace PLDUOBAO
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
#if DEBUG
            LogSys.DebugMode = true;
            LogSys.Create();
#endif

            if (CJKPathGuard.ContainsCJK(AppContext.BaseDirectory))
            {
                try
                {
                    Win32NativeAPI.MessageBox(IntPtr.Zero,
                        $"当前程序所在路径包含中文，请移动至无中文路径后重试。\n\n当前路径：\n{AppContext.BaseDirectory}",
                        "PLDUOBAO",
                        0x00000010 | 0x00000000);
                }
                catch { }
                Environment.Exit(1);
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception; 
                string info = ex != null ? ex.ToString() : (e.ExceptionObject?.ToString() ?? "Unknown error");
                try
                {
                    Win32NativeAPI.MessageBox(IntPtr.Zero,
                        $"程序发生了严重错误，已生成 crash.log\n\n请将该文件发送给开发者以便修复问题。\n\n错误：{info}",
                        "程序崩溃",
                        0x00000010 /* MB_ICONERROR */ | 0x00000000 /* MB_OK */);
                }
                catch { /* 忽略提示失败 */ }
                File.AppendAllText("crash.log", $"[Unhandled] {info}\n");
            };

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
