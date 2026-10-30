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

            var proc = Process.Start(new ProcessStartInfo(exePath)
            {
                FileName = exePath,
                Arguments = " config=\"" + argumentsPath + "\"",
                UseShellExecute = showConsole,
                CreateNoWindow = !showConsole,
            });
            
            // 开始跟踪游戏时长
            GamePlayTracker.StartTracking(proc, serverName ?? "未知服务器", serverType);
            
            return proc;
        }
    }
}
