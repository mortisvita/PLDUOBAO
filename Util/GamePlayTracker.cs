using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Util
{
    /// <summary>
    /// 游戏时长跟踪器
    /// </summary>
    internal static class GamePlayTracker
    {
        private static DateTime? _currentSessionStart;
        private static string? _currentServerName;
        private static string? _currentServerType;
        private static Process? _currentProcess;
        private static CancellationTokenSource? _monitorCts;

        /// <summary>
        /// 开始跟踪游戏时长
        /// </summary>
        public static void StartTracking(Process? process, string serverName, string serverType)
        {
            if (process == null) return;

            _currentSessionStart = DateTime.Now;
            _currentServerName = serverName;
            _currentServerType = serverType;
            _currentProcess = process;

            // 启动监控任务
            _monitorCts?.Cancel();
            _monitorCts = new CancellationTokenSource();
            _ = MonitorProcessAsync(_monitorCts.Token);
        }

        private static async Task MonitorProcessAsync(CancellationToken ct)
        {
            if (_currentProcess == null) return;

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        _currentProcess.WaitForExit();
                    }
                    catch { }
                }, ct);

                if (!ct.IsCancellationRequested)
                {
                    EndTracking();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                LogSys.Err($"[GamePlayTracker] Monitor error: {ex}");
            }
        }

        /// <summary>
        /// 结束跟踪并记录游戏时长
        /// </summary>
        public static void EndTracking()
        {
            if (_currentSessionStart == null) return;

            var endTime = DateTime.Now;
            var duration = (long)(endTime - _currentSessionStart.Value).TotalSeconds;

            // 只记录超过30秒的游戏时长
            if (duration < 30) 
            {
                Reset();
                return;
            }

            var record = new GamePlayRecord
            {
                ServerName = _currentServerName,
                ServerType = _currentServerType,
                StartTime = _currentSessionStart.Value,
                EndTime = endTime,
                DurationSeconds = duration
            };

            SaveRecord(record);
            Reset();
        }

        private static void Reset()
        {
            _currentSessionStart = null;
            _currentServerName = null;
            _currentServerType = null;
            _currentProcess = null;
        }

        private static void SaveRecord(GamePlayRecord record)
        {
            try
            {
                var config = PathUtils.LoadConfig() ?? new UserConfig();
                config.gamePlayStats ??= new GamePlayStats();
                config.gamePlayStats.RecentRecords ??= new List<GamePlayRecord>();

                // 更新统计
                config.gamePlayStats.TotalPlayTimeSeconds += record.DurationSeconds;
                config.gamePlayStats.TotalGameSessions++;
                config.gamePlayStats.LastPlayTime = record.EndTime;

                // 添加到最近记录（最多保存20条）
                config.gamePlayStats.RecentRecords.Insert(0, record);
                if (config.gamePlayStats.RecentRecords.Count > 20)
                {
                    config.gamePlayStats.RecentRecords = config.gamePlayStats.RecentRecords.Take(20).ToList();
                }

                PathUtils.SaveConfig(config);
                LogSys.Log($"[GamePlayTracker] Saved: {record.ServerName}, Duration: {FormatDuration(record.DurationSeconds)}");
            }
            catch (Exception ex)
            {
                LogSys.Err($"[GamePlayTracker] SaveRecord error: {ex}");
            }
        }

        /// <summary>
        /// 获取游戏时长统计
        /// </summary>
        public static GamePlayStats? GetStats()
        {
            var config = PathUtils.LoadConfig();
            return config?.gamePlayStats;
        }

        /// <summary>
        /// 格式化时长显示
        /// </summary>
        public static string FormatDuration(long totalSeconds)
        {
            if (totalSeconds < 60)
                return $"{totalSeconds}秒";

            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}小时{minutes}分钟";
            else
                return $"{minutes}分钟{seconds}秒";
        }

        /// <summary>
        /// 获取今日游戏时长
        /// </summary>
        public static long GetTodayPlayTime()
        {
            var stats = GetStats();
            if (stats?.RecentRecords == null) return 0;

            var today = DateTime.Today;
            return stats.RecentRecords
                .Where(r => r.StartTime.Date == today)
                .Sum(r => r.DurationSeconds);
        }
    }
}
