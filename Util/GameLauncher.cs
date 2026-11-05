using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Util
{
    internal class GameLauncher
    {
        public static async Task<Process?> StartGameWithAuthAsync(string exePath, string argumentsPath, string? serverName = null, string serverType = "NetGame")
        {
            LogSys.Log($"StartGameWithAuth: type={serverType} server={serverName}");

            return StartGameInternal(exePath, argumentsPath, serverName, serverType);
        }

        [Obsolete("Use StartGameWithAuthAsync instead")]
        public static Process? StartGame(string exePath, string argumentsPath, string? serverName = null, string serverType = "NetGame")
        {
            return StartGameInternal(exePath, argumentsPath, serverName, serverType);
        }

        private static Process? StartGameInternal(string exePath, string argumentsPath, string? serverName = null, string serverType = "NetGame")
        {
            exePath = Path.Combine(exePath, "Minecraft.Windows.exe");

            var config = PathUtils.LoadConfig();
            bool showConsole = config?.showGameConsole ?? false;

            string arguments = " config=\"" + argumentsPath + "\"";

            Process? proc = null;

            if (showConsole)
            {
                // 游戏是 GUI 子系统程序：UseShellExecute / CreateNoWindow 都不会给它控制台，
                // 必须用 CREATE_NEW_CONSOLE 强制分配一个属于游戏自己的控制台窗口。
                if (Win32NativeAPI.TryStartWithNewConsole(exePath, arguments, out int pid, out int err))
                {
                    try
                    {
                        proc = Process.GetProcessById(pid);
                        LogSys.Log($"已带独立控制台启动游戏，PID={pid}");
                    }
                    catch (Exception ex)
                    {
                        LogSys.Err($"取得游戏进程失败（PID={pid}）：{ex.Message}");
                    }
                }
                else
                {
                    LogSys.Err($"CREATE_NEW_CONSOLE 启动失败，Win32 错误码={err}，退回无控制台启动");
                }
            }

            if (proc == null)
            {
                proc = Process.Start(new ProcessStartInfo(exePath)
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }

            if (proc == null)
                return null;

            // 开始跟踪游戏时长
            GamePlayTracker.StartTracking(proc, serverName ?? "未知服务器", serverType);

            return proc;
        }
    }
}
