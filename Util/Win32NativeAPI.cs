using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PLDUOBAO.Util
{
    internal partial class Win32NativeAPI
    {
        [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
        public static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        private const uint CREATE_NEW_CONSOLE = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved;
            public string? lpDesktop;
            public string? lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(
            string? lpApplicationName,
            StringBuilder lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// 以 CREATE_NEW_CONSOLE 方式启动进程，让游戏拥有自己的控制台窗口。
        /// 游戏是 GUI 子系统程序，ProcessStartInfo 的 UseShellExecute / CreateNoWindow
        /// 对 GUI 程序都不会产生控制台，必须用该标志强制分配。
        /// </summary>
        /// <param name="processId">成功时返回游戏自身的 PID（设备伪装需要用到）。</param>
        public static bool TryStartWithNewConsole(string exePath, string arguments, out int processId, out int lastError)
        {
            processId = 0;
            lastError = 0;

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf<STARTUPINFO>();

            // CreateProcessW 会就地修改命令行缓冲区，必须传入可写副本
            var commandLine = new StringBuilder("\"" + exePath + "\"" + arguments);

            string? workingDir = Path.GetDirectoryName(exePath);

            bool ok = CreateProcess(
                exePath,
                commandLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CREATE_NEW_CONSOLE,
                IntPtr.Zero,
                workingDir,
                ref si,
                out PROCESS_INFORMATION pi);

            if (!ok)
            {
                lastError = Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                processId = pi.dwProcessId;
            }
            finally
            {
                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
            }

            return true;
        }
    }
}
