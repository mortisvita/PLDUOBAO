using System;
using System.IO;
using System.Security.Cryptography;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Util
{
    // 持久化设备标识。
    // 问题背景
    // --------
    // 之前每次登录：
    // - MgbService.GenerateSAuth 用 Guid.NewGuid() 生成新的 deviceid / udid / client_login_sn
    // - PESADataGenerator 又用另一套随机的 udid / first_udid / mac_addr
    // 两个来源互不一致，且每次都变。4399 风控据此把每次登录判成「新设备异常登录」
    // （mgbsdk uni_sauth 回 code 403 / subcode 15 / login abnormally，要求短信验证），
    // 之后 pe-authentication 只能拿到"服务器维护中"这种被掩盖的错误。
    // 解决
    // ----
    // 把 udid / mac / clientLoginSn 落到 userconfig.ini，跨会话复用；
    // 渠道 sauth、sa_data、原生伪装工具三处统一读这一份值。
    internal static class DeviceIdentity
    {
        private static readonly object _sync = new();
        private static bool _initialized;

        // 16 位小写 hex（与真实 Android udid 同格式）。
        public static string Udid { get; private set; } = string.Empty;

        // 02:00:00:XX:XX:XX。
        public static string Mac { get; private set; } = string.Empty;

        // 32 位大写 hex，渠道 client_login_sn。
        public static string ClientLoginSn { get; private set; } = string.Empty;

        // 从配置载入；缺失则生成并写回。必须在任何登录/启动游戏动作之前调用。
        public static void Ensure()
        {
            lock (_sync)
            {
                if (_initialized) return;

                var cfg = PathUtils.LoadConfig() ?? new UserConfig();
                bool dirty = false;

                if (!IsValidUdid(cfg.deviceUdid))
                {
                    cfg.deviceUdid = NewUdid();
                    dirty = true;
                }
                if (!IsValidMac(cfg.deviceMac))
                {
                    cfg.deviceMac = NewMac();
                    dirty = true;
                }
                if (!IsValidSn(cfg.clientLoginSn))
                {
                    cfg.clientLoginSn = NewSn();
                    dirty = true;
                }

                if (dirty) PathUtils.SaveConfig(cfg);

                Udid = cfg.deviceUdid!;
                Mac = cfg.deviceMac!;
                ClientLoginSn = cfg.clientLoginSn!;
                _initialized = true;
            }
        }

        private static bool IsValidUdid(string? s) =>
            !string.IsNullOrWhiteSpace(s) && s.Length == 16 && IsHex(s);

        private static bool IsValidMac(string? s) =>
            !string.IsNullOrWhiteSpace(s) && s.Length == 17 && s.StartsWith("02:00:00:", StringComparison.Ordinal);

        private static bool IsValidSn(string? s) =>
            !string.IsNullOrWhiteSpace(s) && s.Length == 32 && IsHex(s);

        private static bool IsHex(string s)
        {
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        private static string NewUdid()
        {
            Span<byte> buf = stackalloc byte[8];
            RandomNumberGenerator.Fill(buf);
            return Convert.ToHexString(buf).ToLowerInvariant();
        }

        private static string NewMac()
        {
            Span<byte> buf = stackalloc byte[3];
            RandomNumberGenerator.Fill(buf);
            return $"02:00:00:{buf[0]:X2}:{buf[1]:X2}:{buf[2]:X2}";
        }

        private static string NewSn() => Guid.NewGuid().ToString("N").ToUpper();
    }
}
