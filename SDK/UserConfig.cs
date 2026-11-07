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
    
    // 皮肤历史记录
    public class SkinHistoryItem
    {
        public string? Path { get; set; }
        public bool IsSlim { get; set; }
        public DateTime LastUsed { get; set; }
    }
    
    // 游戏时长记录项
    public class GamePlayRecord
    {
        public string? ServerName { get; set; }
        public string? ServerType { get; set; } // "Local" | "NetGame" | "Rental"
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public long DurationSeconds { get; set; }
    }
    
    // 游戏时长统计
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

        // 首次启动主题引导是否已完成（写入 userconfig.ini）
        public bool firstLaunchDone { get; set; }
        
        // 自定义强调色 (ARGB 格式，null 表示跟随系统)
        public uint? customAccentColor { get; set; }
        
        // 是否使用自定义强调色
        public bool useCustomAccentColor { get; set; } = false;


        // 游戏控制台窗口设置
        public bool showGameConsole { get; set; } = false;

        // 设备绕过设置
        public bool enableDeviceSpoof { get; set; } = true;

        // 选定的设备品牌。为空表示随机。
        public string? deviceBrand { get; set; }

        // 选定的设备型号 Key。为空或 random 时在该品牌内随机。
        public string? deviceModelKey { get; set; }

        // 持久化的设备标识（16 位小写 hex）。
        // 渠道 sauth 的 deviceid/udid、sa_data 的 udid/first_udid 共用这个值。
        // 每次登录重新随机会被 4399 判定为「新设备异常登录」(403/subcode 15)，
        // 触发短信安全验证，因此必须跨会话固定。
        public string? deviceUdid { get; set; }

        // 与 deviceUdid 配对的 MAC（02:00:00:XX:XX:XX）。
        public string? deviceMac { get; set; }

        // 持久化的渠道 client_login_sn（32 位大写 hex）。
        // 同样用于避免每次登录都被当作新会话。
        public string? clientLoginSn { get; set; }

        public bool usePreferredGameDownloadVersions { get; set; } = false;
        public string? gameDownloadPath { get; set; }
        
        // 游戏下载路径历史 (最多保存 10 条，用于下拉选择)
        
        // 皮肤历史记录 (最多保存 10 条)
        public List<SkinHistoryItem>? skinHistory { get; set; }
        
        // 游戏时长统计
        public GamePlayStats? gamePlayStats { get; set; }
    }
}
