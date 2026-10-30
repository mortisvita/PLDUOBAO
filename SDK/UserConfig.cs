using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK;

namespace PLDUOBAO.SDK
{
    public enum AppTheme
    {
        Light = 0,
        Dark = 1,
        System = 2
    }
    
    /// <summary>
    /// 皮肤历史记录
    /// </summary>
    public class SkinHistoryItem
    {
        public string? Path { get; set; }
        public bool IsSlim { get; set; }
        public DateTime LastUsed { get; set; }
    }
    
    /// <summary>
    /// 游戏时长记录项
    /// </summary>
    public class GamePlayRecord
    {
        public string? ServerName { get; set; }
        public string? ServerType { get; set; } // "Local" | "NetGame" | "Rental"
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long DurationSeconds { get; set; }
    }
    
    /// <summary>
    /// 游戏时长统计
    /// </summary>
    public class GamePlayStats
    {
        public long TotalPlayTimeSeconds { get; set; }
        public int TotalGameSessions { get; set; }
        public DateTime? LastPlayTime { get; set; }
        public List<GamePlayRecord>? RecentRecords { get; set; }
    }
    
    [JsonSerializable(typeof(UserConfig))]
    [JsonSerializable(typeof(List<SkinHistoryItem>))]
    [JsonSerializable(typeof(GamePlayStats))]
    [JsonSerializable(typeof(List<GamePlayRecord>))]
    internal partial class UserConfigContext : JsonSerializerContext { }
    internal class UserConfig
    {
        public string? userGamePath { get; set; }
        public string? userSkinPath { get; set; }
        public bool? isSkinSlim { get; set; }
        
        // 背景透明度设置 (0.0 - 1.0)
        public double backgroundOpacity { get; set; } = 0.95;
        
        // 主题设置
        public AppTheme theme { get; set; } = AppTheme.System;

        /// <summary>首次启动主题引导是否已完成（写入 userconfig.ini）</summary>
        public bool firstLaunchDone { get; set; }
        
        // 自定义强调色 (ARGB 格式，null 表示跟随系统)
        public uint? customAccentColor { get; set; }
        
        // 是否使用自定义强调色
        public bool useCustomAccentColor { get; set; } = false;


        // 游戏控制台窗口设置
        public bool showGameConsole { get; set; } = false;

        // 设备绕过设置
        public bool enableDeviceSpoof { get; set; } = true;

        public bool usePreferredGameDownloadVersions { get; set; } = false;
        public string? gameDownloadPath { get; set; }
        
        // 游戏下载路径历史 (最多保存 10 条，用于下拉选择)
        
        // 皮肤历史记录 (最多保存 10 条)
        public List<SkinHistoryItem>? skinHistory { get; set; }
        
        // 游戏时长统计
        public GamePlayStats? gamePlayStats { get; set; }
    }
}
