using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PLDUOBAO.Util
{
    // 全局统一的设备档案（Android 移动端）。
    // 网络服（布吉岛等）会把两处上报的设备信息交叉比对：
    // 1. sa_data           —— pe-authentication 请求体里的设备 JSON
    // 2. 客户端上报        —— PlatformPatcher 在进程内存里改写的平台标识
    // 任意一处把自己声明成 PC、或型号与平台对不上，服务端就把客户端当成非移动端，
    // 于是不下发自定义模型（客户端表现为紫黑方块、皮肤不渲染），
    // 并且匹配队列会落到"互通队列"。
    // 所以档案只生成一次，各处共用同一份值。
    internal sealed class DeviceProfile
    {
        public required string Brand { get; init; }

        // 设备名（中文，例如「小米 13」「红米 K60」）。
        public required string Model { get; init; }

        // 厂商内部型号代码（例如 2211133C），sa_data 上报用。
        public required string ModelCode { get; init; }

        public required int DeviceWidth { get; init; }
        public required int DeviceHeight { get; init; }
        public required string OsName { get; init; }
        public required string OsVersion { get; init; }
        public required string CpuName { get; init; }
        public required string CpuHz { get; init; }
        public required int CoreNum { get; init; }
        public required long RamBytes { get; init; }
        public required long RomBytes { get; init; }

        // 16 位小写 hex。每次选定设备时重新随机生成。
        public required string Udid { get; init; }

        // 02:00:00:XX:XX:XX。每次选定设备时重新随机生成。
        public required string MacAddress { get; init; }

        // 机型池里的稳定标识。
        public required string Key { get; init; }

        private static readonly object _sync = new();
        private static DeviceProfile? _current;

        // 本次运行期间固定的设备档案。
        public static DeviceProfile Current
        {
            get
            {
                if (_current is not null) return _current;
                lock (_sync)
                {
                    if (_current is null)
                    {
                        // 默认档案同样使用持久化标识，保证只调用 Current（未显式 Select）
                        // 时 sa_data 的 udid 仍与渠道 sauth 一致。
                        DeviceIdentity.Ensure();
                        _current = Create(null, null, DeviceIdentity.Udid, DeviceIdentity.Mac);
                    }
                }
                return _current;
            }
        }

        // 按设备重新选定档案。必须在启动游戏前调用。
        public static DeviceProfile Select(string? deviceKey, string? brand = null)
        {
            lock (_sync)
            {
                _current = Create(deviceKey, brand);
                return _current;
            }
        }

        // 按设备重新选定档案，但强制使用持久化的 udid / mac。
        // 用于登录链路：渠道 sauth 与 sa_data 必须用同一份设备标识，且要跨会话
        // 固定，否则每次登录都被判成新设备（4399 403/subcode 15）。
        public static DeviceProfile SelectPersistent(string? deviceKey, string? brand,
                                                     string udid, string mac)
        {
            lock (_sync)
            {
                _current = Create(deviceKey, brand, udid, mac);
                return _current;
            }
        }

        public static IReadOnlyList<DeviceEntry> Pool => AndroidPool;

        public static IReadOnlyList<string> Brands =>
            AndroidPool.Select(e => e.Brand)
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(b => b, StringComparer.OrdinalIgnoreCase)
                       .ToList();

        public static IReadOnlyList<DeviceEntry> ModelsOf(string brand) =>
            AndroidPool.Where(e => string.Equals(e.Brand, brand, StringComparison.OrdinalIgnoreCase))
                       .OrderBy(e => e.Model, StringComparer.OrdinalIgnoreCase)
                       .ToList();

        private static DeviceProfile Create(string? key, string? brand,
                                            string? forcedUdid = null, string? forcedMac = null)
        {
            DeviceEntry pick;

            bool wantRandom = string.IsNullOrWhiteSpace(key)
                              || key.Equals("random", StringComparison.OrdinalIgnoreCase);

            if (wantRandom)
            {
                var candidates = string.IsNullOrWhiteSpace(brand)
                    ? AndroidPool
                    : AndroidPool.Where(e => string.Equals(e.Brand, brand, StringComparison.OrdinalIgnoreCase))
                                 .ToArray();

                if (candidates.Length == 0)
                {
                    candidates = AndroidPool;
                }

                pick = candidates[Random.Shared.Next(candidates.Length)];
            }
            else
            {
                var match = AndroidPool.FirstOrDefault(
                    e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                      || e.Model.Equals(key, StringComparison.OrdinalIgnoreCase));

                pick = match ?? AndroidPool[Random.Shared.Next(AndroidPool.Length)];
            }

            return new DeviceProfile
            {
                Key = pick.Key,
                Brand = pick.Brand,
                Model = pick.Model,
                ModelCode = pick.ModelCode,
                DeviceWidth = pick.Width,
                DeviceHeight = pick.Height,
                OsName = "android",
                OsVersion = pick.OsVersion,
                CpuName = pick.Cpu,
                CpuHz = pick.CpuHz,
                CoreNum = pick.Cores,
                RamBytes = pick.RamGb * 1024L * 1024 * 1024,
                RomBytes = pick.RomGb * 1024L * 1024 * 1024,
                Udid = string.IsNullOrWhiteSpace(forcedUdid) ? RandomHex16() : forcedUdid,
                MacAddress = string.IsNullOrWhiteSpace(forcedMac) ? RandomLocalMac() : forcedMac,
            };
        }

        // 16 位小写 hex，与真实 udid 同格式。每次调用都不同。
        public static string RandomHex16()
        {
            Span<byte> buf = stackalloc byte[8];
            RandomNumberGenerator.Fill(buf);
            return Convert.ToHexString(buf).ToLowerInvariant();
        }

        // 02:00:00 段（本地管理地址），真实设备随机化后就在这个段。
        public static string RandomLocalMac()
        {
            Span<byte> buf = stackalloc byte[3];
            RandomNumberGenerator.Fill(buf);
            return $"02:00:00:{buf[0]:X2}:{buf[1]:X2}:{buf[2]:X2}";
        }

        // ===================================================================
        // 机型池
        //
        // 收录条件：
        //   1. 发布年份 2022 – 2026
        //   2. Android 版本 >= 12
        //   3. 运行内存 >= 8 GB（含 12G / 16G 版本）
        //
        // Model 是用户看到的中文设备名（小米 13、红米 K60），
        // ModelCode 是厂商内部型号（2211133C），两者都要自洽：
        // 屏幕分辨率、SoC 主频、核心数、存储必须与机型匹配，
        // 否则会被服务端识别成伪造设备。
        // ===================================================================
        private static readonly DeviceEntry[] AndroidPool =
        {
            // ---------------- 小米 ----------------
            new("小米", "小米 12",      "2201122C",  1440, 3200, "13", "Qualcomm Snapdragon 8 Gen 1",  "3000000", 8,  8, 256, "xiaomi-12"),
            new("小米", "小米 12 Pro",  "2201122G",  1440, 3200, "13", "Qualcomm Snapdragon 8 Gen 1",  "3000000", 8,  8, 256, "xiaomi-12-pro"),
            new("小米", "小米 12S",     "2206123SC", 1080, 2400, "13", "Qualcomm Snapdragon 8+ Gen 1", "3190000", 8,  8, 256, "xiaomi-12s"),
            new("小米", "小米 13",      "2211133C",  1080, 2400, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8,  8, 256, "xiaomi-13"),
            new("小米", "小米 13 Pro",  "2210132C",  1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8,  8, 256, "xiaomi-13-pro"),
            new("小米", "小米 13 Ultra","2304FPN6DC",1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8, 12, 512, "xiaomi-13-ultra"),
            new("小米", "小米 14",      "23127PN0CC",1200, 2670, "14", "Qualcomm Snapdragon 8 Gen 3",  "3300000", 8,  8, 256, "xiaomi-14"),
            new("小米", "小米 14 Pro",  "23116PN5BC",1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 3",  "3300000", 8, 12, 256, "xiaomi-14-pro"),
            new("小米", "小米 14 Ultra","2405CPX3DC",1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 3",  "3300000", 8, 16, 512, "xiaomi-14-ultra"),
            new("小米", "小米 15",      "24129PN74C",1200, 2670, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 256, "xiaomi-15"),
            new("小米", "小米 15 Pro",  "2405CPX3DG",1440, 3200, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 16, 512, "xiaomi-15-pro"),
            new("小米", "小米 15 Ultra","25019PNF3C",1440, 3200, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 16, 512, "xiaomi-15-ultra"),
            new("小米", "小米 16",      "25113PN0EC",1200, 2670, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "xiaomi-16"),
            new("小米", "小米 16 Pro",  "25113PN0EG",1440, 3200, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "xiaomi-16-pro"),
            new("小米", "小米 16 Ultra","26019PNF3C",1440, 3200, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "xiaomi-16-ultra"),
            new("小米", "小米 17",        "26113PN0EA",1200, 2670, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "xiaomi-17"),
            new("小米", "小米 17 Pro",    "26113PN0EB",1200, 2670, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 512, "xiaomi-17-pro"),
            new("小米", "小米 17 Pro Max","26113PN0EC",1440, 3200, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "xiaomi-17-pro-max"),
            new("小米", "小米 17 Ultra",  "26113PN0ED",1440, 3200, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 1024, "xiaomi-17-ultra"),
            new("小米", "小米 18",      "26113PN0FG",1200, 2670, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 12, 256, "xiaomi-18"),
            new("小米", "小米 18 Pro",  "26113PN0FH",1440, 3200, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "xiaomi-18-pro"),
            new("小米", "小米 18 Pro Max","26113PN0FJ",1440, 3200, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "xiaomi-18-pro-max"),
            new("小米", "小米 18 Fold", "26113PN0FK",1812, 2176, "17", "Xring O3",                     "3600000", 8, 16, 512, "xiaomi-18-fold"),
            new("小米", "小米 18 Ultra","26119PNF3C",1440, 3200, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "xiaomi-18-ultra"),
            new("小米", "小米 MIX Flip 2","2505APX7BC",1080,2520,"15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 256, "xiaomi-mix-flip2"),
            new("小米", "小米 MIX Fold 4","2405CPX3DE",1812,2176,"14","Qualcomm Snapdragon 8 Gen 3",  "3300000", 8, 16, 512, "xiaomi-mix-fold4"),
            new("小米", "小米 MIX Fold 5","2505APX7CC",1812,2176,"15","Qualcomm Snapdragon 8 Elite",  "4320000", 8, 16, 512, "xiaomi-mix-fold5"),
            new("小米", "小米 Civi 4 Pro","24053PY09C",1200,2670, "14", "Qualcomm Snapdragon 8s Gen 3", "3000000", 8, 12, 256, "xiaomi-civi4pro"),
            new("小米", "小米 Civi 5 Pro","25057PY09C",1200,2670, "15", "Qualcomm Snapdragon 8s Gen 4", "3100000", 8, 12, 256, "xiaomi-civi5pro"),
            new("小米", "小米 Civi 6",   "26057PY09C",1200,2670, "17", "Qualcomm Snapdragon 8s Gen 4", "3100000", 8, 12, 256, "xiaomi-civi6"),

            // ---------------- 红米 ----------------
            new("红米", "红米 K50",       "22041211AC", 1080, 2400, "13", "MediaTek Dimensity 8100",     "2850000", 8,  8, 128, "redmi-k50"),
            new("红米", "红米 K50 Pro",   "22011211C",  1440, 3200, "13", "MediaTek Dimensity 9000",     "3050000", 8,  8, 256, "redmi-k50-pro"),
            new("红米", "红米 K50 至尊版","22061218C",  1080, 2400, "13", "Qualcomm Snapdragon 8+ Gen 1","3190000", 8,  8, 256, "redmi-k50-ultra"),
            new("红米", "红米 K60",       "23013RK75C", 1220, 2712, "13", "Qualcomm Snapdragon 8+ Gen 1","3190000", 8,  8, 256, "redmi-k60"),
            new("红米", "红米 K60 Pro",   "22127RK46C", 1440, 3200, "13", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "redmi-k60-pro"),
            new("红米", "红米 K60 至尊版","23078RKD5C", 1220, 2712, "14", "MediaTek Dimensity 9200+",    "3350000", 8, 12, 512, "redmi-k60-ultra"),
            new("红米", "红米 K70",       "23113RKC6C", 1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "redmi-k70"),
            new("红米", "红米 K70 Pro",   "23117RK66C", 1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "redmi-k70-pro"),
            new("红米", "红米 K70 至尊版","2407FRK8EC", 1220, 2712, "14", "MediaTek Dimensity 9300+",    "3400000", 8, 12, 256, "redmi-k70-ultra"),
            new("红米", "红米 K80",       "24117RK2CC", 1200, 2670, "15", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "redmi-k80"),
            new("红米", "红米 K80 Pro",   "24122RKC7C", 1200, 2670, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 512, "redmi-k80-pro"),
            new("红米", "红米 K80 至尊版","25060RK16C", 1220, 2712, "15", "MediaTek Dimensity 9400+",    "3730000", 8, 12, 256, "redmi-k80-ultra"),
            new("红米", "红米 K90",       "25113RK58C", 1200, 2670, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "redmi-k90"),
            new("红米", "红米 K90 Pro",   "25113RK58G", 1200, 2670, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "redmi-k90-pro"),
            new("红米", "红米 K90 至尊版","26013RK58C", 1220, 2712, "16", "MediaTek Dimensity 9500",     "4130000", 8, 12, 256, "redmi-k90-ultra"),
            new("红米", "红米 K90 Max",   "26013RK58M", 1220, 2712, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 1024, "redmi-k90-max"),
            new("红米", "红米 K90 Pro Max","25113RK58P",1440, 3200, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "redmi-k90-pro-max"),
            new("红米", "红米 K100",      "26113RK58C", 1220, 2712, "16", "MediaTek Dimensity 9500+",    "4200000", 8, 12, 512, "redmi-k100"),
            new("红米", "红米 K100 Pro",  "26103RK58C", 1200, 2670, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "redmi-k100-pro"),
            new("红米", "红米 K100 Pro Max","26103RK58D",1440, 3200, "16", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "redmi-k100-pro-max"),
            new("红米", "红米 Note 12 Turbo","23049RAD8C",1220,2712,"13","Qualcomm Snapdragon 7+ Gen 2","2918400", 8, 8, 256, "redmi-note12-turbo"),
            new("红米", "红米 Note 12 Pro","22101316C", 1080, 2400, "13", "MediaTek Dimensity 1080",     "2600000", 8,  8, 256, "redmi-note12-pro"),
            new("红米", "红米 Note 13 Pro","23054RA19C",1080, 2400, "13", "MediaTek Dimensity 7200",     "2800000", 8,  8, 256, "redmi-note13-pro"),
            new("红米", "红米 Note 13 Pro+","23013RKA1C",1220,2712,"14", "MediaTek Dimensity 7200 Ultra","2800000", 8, 12, 512, "redmi-note13-pro-plus"),
            new("红米", "红米 Note 14 Pro","24094RAD4C",1080, 2400, "14", "MediaTek Dimensity 7300",     "2500000", 8,  8, 256, "redmi-note14-pro"),
            new("红米", "红米 Note 14 Pro+","24094RAD4G",1220,2712,"14", "Qualcomm Snapdragon 7s Gen 3","2400000", 8, 12, 256, "redmi-note14-pro-plus"),
            new("红米", "红米 Note 15 Pro","25078RA3EC",1080, 2400, "16", "MediaTek Dimensity 7400",     "2600000", 8,  8, 256, "redmi-note15-pro"),
            new("红米", "红米 Note 15 Pro+","25078RA3EG",1220,2712,"16","Qualcomm Snapdragon 7s Gen 4",  "2600000", 8, 12, 256, "redmi-note15-pro-plus"),
            new("红米", "红米 13C",       "23124RN87C", 1080, 2400, "14", "MediaTek Dimensity 6100+",    "2200000", 8,  8, 128, "redmi-13c"),
            new("红米", "红米 14C",       "2409BRN2CC", 1080, 2400, "15", "MediaTek Dimensity 6300",     "2400000", 8,  8, 256, "redmi-14c"),
            new("红米", "红米 15C",       "25099RN6CC", 1080, 2400, "16", "MediaTek Dimensity 6400",     "2400000", 8,  8, 256, "redmi-15c"),
            new("红米", "红米 Turbo 4",   "25053RT47C", 1220, 2712, "15", "MediaTek Dimensity 8400 Ultra","3250000", 8, 12, 256, "redmi-turbo4"),
            new("红米", "红米 Turbo 4 Pro","25053RT47G",1220, 2712, "15", "Qualcomm Snapdragon 8s Gen 4","3100000", 8, 12, 256, "redmi-turbo4-pro"),

            // ---------------- POCO ----------------
            new("POCO", "POCO F4",        "22021211RG", 1080, 2400, "13", "Qualcomm Snapdragon 870",     "3200000", 8,  8, 256, "poco-f4"),
            new("POCO", "POCO X4 GT",     "2207117BPG", 1080, 2400, "13", "MediaTek Dimensity 8100",     "2850000", 8,  8, 256, "poco-x4-gt"),
            new("POCO", "POCO X6 Pro",    "23013PC75G", 1080, 2400, "13", "MediaTek Dimensity 8200",     "3100000", 8,  8, 256, "poco-x6-pro"),
            new("POCO", "POCO X6 5G",     "23122PCD1G", 1220, 2712, "14", "Qualcomm Snapdragon 4 Gen 2", "2200000", 8,  8, 256, "poco-x6-5g"),
            new("POCO", "POCO X6 Pro 5G", "24069PC21G", 1220, 2712, "14", "MediaTek Dimensity 7200 Ultra","2800000", 8, 12, 512, "poco-x6-pro-plus"),
            new("POCO", "POCO F6 Pro",    "2405CPX3DH", 1440, 3200, "14", "Qualcomm Snapdragon 8s Gen 3","3000000", 8, 12, 256, "poco-f6-pro"),
            new("POCO", "POCO F7",        "25053PC47G", 1220, 2712, "15", "MediaTek Dimensity 8400 Ultra","3250000", 8, 12, 256, "poco-f7"),
            new("POCO", "POCO F7 Pro",    "25053PC47H", 1200, 2670, "15", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "poco-f7-pro"),
            new("POCO", "POCO F7 Ultra",  "25053PC47J", 1200, 2670, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "poco-f7-ultra"),
            new("POCO", "POCO X7 Pro",    "24095PC47G", 1220, 2712, "15", "MediaTek Dimensity 8400 Ultra","3250000", 8, 12, 256, "poco-x7-pro"),
            new("POCO", "POCO X7",        "24095PC47H", 1080, 2400, "15", "MediaTek Dimensity 7300 Ultra","2500000", 8,  8, 256, "poco-x7"),
            new("POCO", "POCO X8 Pro",    "25095PC47G", 1220, 2712, "16", "MediaTek Dimensity 8500",     "3300000", 8, 12, 256, "poco-x8-pro"),

            // ---------------- 三星 ----------------
            new("三星", "Galaxy S22",      "SM-S9010", 1080, 2340, "14", "Qualcomm Snapdragon 8 Gen 1",  "3000000", 8,  8, 128, "galaxy-s22"),
            new("三星", "Galaxy S22+",     "SM-S9060", 1080, 2340, "14", "Qualcomm Snapdragon 8 Gen 1",  "3000000", 8,  8, 128, "galaxy-s22-plus"),
            new("三星", "Galaxy S23",      "SM-S9110", 1080, 2340, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8,  8, 256, "galaxy-s23"),
            new("三星", "Galaxy S23 Ultra","SM-S9180", 1440, 3088, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8, 12, 256, "galaxy-s23-ultra"),
            new("三星", "Galaxy S24",      "SM-S9210", 1080, 2340, "14", "Qualcomm Snapdragon 8 Gen 3",  "3300000", 8,  8, 256, "galaxy-s24"),
            new("三星", "Galaxy S24 Ultra","SM-S9280", 1440, 3120, "14", "Qualcomm Snapdragon 8 Gen 3",  "3300000", 8, 12, 256, "galaxy-s24-ultra"),
            new("三星", "Galaxy S25",      "SM-S9310", 1080, 2340, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 256, "galaxy-s25"),
            new("三星", "Galaxy S25+",     "SM-S9360", 1440, 3120, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 256, "galaxy-s25-plus"),
            new("三星", "Galaxy S25 Ultra","SM-S9380", 1440, 3120, "15", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 512, "galaxy-s25-ultra"),
            new("三星", "Galaxy S26",      "SM-S9410", 1080, 2340, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "galaxy-s26"),
            new("三星", "Galaxy S26 Ultra","SM-S9480", 1440, 3120, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "galaxy-s26-ultra"),
            new("三星", "Galaxy S27 Ultra","SM-S9580", 1440, 3120, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "galaxy-s27-ultra"),
            new("三星", "Galaxy Z Fold7",  "SM-F9660", 1812, 2176, "16", "Qualcomm Snapdragon 8 Elite",  "4320000", 8, 12, 256, "galaxy-z-fold7"),
            new("三星", "Galaxy A56",      "SM-A5660", 1080, 2340, "15", "Samsung Exynos 1580",          "2900000", 8,  8, 256, "galaxy-a56"),
            new("三星", "Galaxy A36",      "SM-A3660", 1080, 2340, "15", "Qualcomm Snapdragon 6 Gen 3",  "2400000", 8,  8, 128, "galaxy-a36"),
            new("三星", "Galaxy A53",      "SM-A5360", 1080, 2400, "14", "Samsung Exynos 1280",          "2400000", 8,  8, 128, "galaxy-a53"),
            new("三星", "Galaxy A54",      "SM-A5460", 1080, 2340, "14", "Samsung Exynos 1380",          "2400000", 8,  8, 256, "galaxy-a54"),
            new("三星", "Galaxy A55",      "SM-A5560", 1080, 2340, "14", "Samsung Exynos 1480",          "2750000", 8,  8, 256, "galaxy-a55"),
            new("三星", "Galaxy Z Flip4",  "SM-F7210", 1080, 2640, "13", "Qualcomm Snapdragon 8+ Gen 1",  "3190000", 8,  8, 256, "galaxy-z-flip4"),
            new("三星", "Galaxy Z Flip5",  "SM-F7310", 1080, 2640, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8,  8, 256, "galaxy-z-flip5"),
            new("三星", "Galaxy Z Fold5",  "SM-F9460", 1812, 2176, "14", "Qualcomm Snapdragon 8 Gen 2",  "3200000", 8, 12, 256, "galaxy-z-fold5"),

            // ---------------- OPPO / 一加 / realme ----------------
            new("OPPO",  "Find X6",       "PHB110", 1440, 3168, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8,  8, 256, "find-x6"),
            new("OPPO",  "Find X6 Pro",   "PGFM10", 1240, 2772, "14", "MediaTek Dimensity 9200",     "3350000", 8, 12, 256, "find-x6-pro"),
            new("OPPO",  "Find X7",       "PJD110", 1264, 2780, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "find-x7"),
            new("OPPO",  "Find X7 Ultra", "PHY110", 1440, 3168, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 16, 512, "find-x7-ultra"),
            new("OPPO",  "Reno 11 Pro",   "PJH110", 1080, 2412, "14", "MediaTek Dimensity 8200",     "3100000", 8, 12, 256, "oppo-reno11-pro"),
            new("OPPO",  "Find X8",       "PKB110", 1264, 2780, "15", "MediaTek Dimensity 9400",     "3620000", 8, 12, 256, "find-x8"),
            new("OPPO",  "Find X8 Pro",   "PKC110", 1264, 2780, "15", "MediaTek Dimensity 9400",     "3620000", 8, 16, 512, "find-x8-pro"),
            new("OPPO",  "Find X8 Ultra", "PKJ110", 1440, 3168, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "find-x8-ultra"),
            new("OPPO",  "Find X9",       "PMA110", 1264, 2780, "16", "MediaTek Dimensity 9500",     "4130000", 8, 12, 256, "find-x9"),
            new("OPPO",  "Find X9 Pro",   "PMA120", 1264, 2780, "16", "MediaTek Dimensity 9500",     "4130000", 8, 16, 512, "find-x9-pro"),
            new("OPPO",  "Find X9 Ultra", "PMA130", 1440, 3168, "16", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "find-x9-ultra"),
            new("OPPO",  "Find X10",      "PMB110", 1264, 2780, "17", "MediaTek Dimensity 9600 Pro", "4400000", 8, 12, 256, "find-x10"),
            new("OPPO",  "Reno 13 Pro",   "PKK110", 1080, 2412, "15", "MediaTek Dimensity 8350",     "3350000", 8, 12, 256, "oppo-reno13-pro"),
            new("OPPO",  "Reno 14 Pro",   "PKZ110", 1080, 2412, "16", "MediaTek Dimensity 8450",     "3400000", 8, 12, 256, "oppo-reno14-pro"),
            new("一加",  "一加 Ace 2",    "PHK110", 1080, 2412, "14", "Qualcomm Snapdragon 8+ Gen 1","3190000", 8, 12, 256, "oneplus-ace-2"),
            new("一加",  "一加 Ace",      "PJG110", 1080, 2412, "14", "MediaTek Dimensity 8100",     "2850000", 8, 12, 256, "oneplus-ace"),
            new("一加",  "一加 Ace 3",    "PJE120", 1264, 2780, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "oneplus-ace-3"),
            new("一加",  "一加 Ace 5",    "PKG110", 1264, 2780, "15", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "oneplus-ace-5"),
            new("一加",  "一加 Ace 5 Pro","PKR120", 1264, 2780, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 256, "oneplus-ace-5-pro"),
            new("一加",  "一加 14",       "PLR110", 1440, 3168, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "oneplus-14"),
            new("一加",  "一加 16",       "PLS110", 1440, 3168, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "oneplus-16"),
            new("一加",  "一加 Ace 7",    "PKT110", 1264, 2780, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "oneplus-ace-7"),
            new("一加",  "一加 Ace 7 Pro","PMS110", 1264, 2780, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "oneplus-ace-7-pro"),
            new("一加",  "一加 Ace 7T",   "PKU110", 1264, 2780, "17", "MediaTek Dimensity 9500S",      "4300000", 8, 12, 256, "oneplus-ace-7t"),
            new("一加",  "一加 Ace 7 Ultra","PKW110",1440, 3168, "17", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "oneplus-ace-7-ultra"),
            new("一加",  "一加 Ace 6",    "PLR120", 1264, 2780, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "oneplus-ace-6"),
            new("一加",  "一加 11",       "PJE110", 1240, 2772, "13", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "oneplus-11"),
            new("一加",  "一加 12",       "PJZ110", 1440, 3168, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 16, 256, "oneplus-12"),
            new("一加",  "一加 13",       "PKR110", 1440, 3168, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "oneplus-13"),
            new("一加",  "一加 13T",      "PKX110", 1264, 2780, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 256, "oneplus-13t"),
            new("realme","realme GT5",    "RMX3820",1264, 2780, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "realme-gt5"),
            new("realme","realme 11 Pro", "RMX3740",1080, 2412, "14", "MediaTek Dimensity 7050",     "2600000", 8, 12, 256, "realme-11-pro"),
            new("realme","realme 12 Pro", "RMX3907",1080, 2400, "14", "Qualcomm Snapdragon 7s Gen 2","2400000", 8, 12, 256, "realme-12-pro"),
            new("realme","realme 13 Pro", "RMX3997",1080, 2400, "14", "MediaTek Dimensity 7300",     "2500000", 8, 12, 256, "realme-13-pro"),
            new("realme","realme 14 Pro", "RMX5050",1080, 2400, "15", "Qualcomm Snapdragon 7s Gen 3","2400000", 8, 12, 256, "realme-14-pro"),
            new("realme","realme 15 Pro", "RMX5060",1080, 2400, "16", "MediaTek Dimensity 7400",     "2600000", 8, 12, 256, "realme-15-pro"),
            new("realme","realme GT7 Pro","RMX5010",1264, 2780, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 256, "realme-gt7-pro"),
            new("realme","realme GT8 Pro","RMX5090",1264, 2780, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "realme-gt8-pro"),

            // ---------------- vivo / iQOO ----------------
            new("vivo", "vivo X90",      "V2227A", 1440, 3200, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "vivo-x90"),
            new("vivo", "vivo X100",     "V2309A", 1260, 2800, "14", "MediaTek Dimensity 9300",     "3250000", 8, 12, 256, "vivo-x100"),
            new("vivo", "vivo X100 Pro", "V2324A", 1260, 2800, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 16, 512, "vivo-x100-pro"),
            new("vivo", "vivo X200",     "V2359A", 1260, 2800, "15", "MediaTek Dimensity 9400",     "3620000", 8, 12, 256, "vivo-x200"),
            new("vivo", "vivo X200 Pro", "V2417A", 1260, 2800, "15", "MediaTek Dimensity 9400",     "3620000", 8, 16, 512, "vivo-x200-pro"),
            new("vivo", "vivo X200 Ultra","V2455A",1440, 3200, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "vivo-x200-ultra"),
            new("vivo", "vivo X300",     "V2509A", 1260, 2800, "16", "MediaTek Dimensity 9500",     "4130000", 8, 12, 256, "vivo-x300"),
            new("vivo", "vivo X300 Pro", "V2509B", 1260, 2800, "16", "MediaTek Dimensity 9500",     "4130000", 8, 16, 512, "vivo-x300-pro"),
            new("vivo", "vivo X300 Ultra","V2555A", 1440, 3200, "16", "Qualcomm Snapdragon 8 Elite Gen 6", "4600000", 8, 16, 512, "vivo-x300-ultra"),
            new("vivo", "vivo X500 Pro Max","V2709A",1440,3200, "17", "MediaTek Dimensity 9600 Pro",  "4400000", 8, 16, 512, "vivo-x500-pro-max"),
            new("vivo", "vivo Y600",     "V2630A", 1080, 2400, "17", "Qualcomm Snapdragon 6 Gen 4",  "2400000", 8,  8, 256, "vivo-y600"),
            new("vivo", "vivo S18",      "V2283A", 1080, 2388, "14", "MediaTek Dimensity 7200",     "2800000", 8, 12, 256, "vivo-s18"),
            new("vivo", "vivo S20",      "V2429A", 1080, 2388, "15", "Qualcomm Snapdragon 7 Gen 3", "2630000", 8, 12, 256, "vivo-s20"),
            new("vivo", "vivo S16 Pro",  "V2241A", 1080, 2400, "13", "MediaTek Dimensity 8200",     "3100000", 8, 12, 256, "vivo-s16-pro"),
            new("iQOO", "iQOO 12",       "V2302A", 1260, 2800, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "iqoo-12"),
            new("iQOO", "iQOO 13",       "V2408A", 1260, 2800, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 256, "iqoo-13"),
            new("iQOO", "iQOO 15",       "V2505A", 1260, 2800, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "iqoo-15"),
            new("iQOO", "iQOO Neo8 Pro", "V2284A", 1080, 2400, "13", "MediaTek Dimensity 9200+",    "3350000", 8, 12, 256, "iqoo-neo8-pro"),
            new("iQOO", "iQOO Neo9 Pro", "V2331A", 1260, 2800, "15", "MediaTek Dimensity 9300+",    "3400000", 8, 12, 256, "iqoo-neo9-pro"),
            new("iQOO", "iQOO Neo10 Pro","V2425A", 1260, 2800, "15", "MediaTek Dimensity 9400",     "3620000", 8, 12, 256, "iqoo-neo10-pro"),
            new("iQOO", "iQOO Z9 Turbo", "V2352A", 1080, 2400, "15", "MediaTek Dimensity 9300+",    "3400000", 8, 12, 256, "iqoo-z9-turbo"),
            new("iQOO", "iQOO Neo12",    "V2612A", 1440, 3168, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "iqoo-neo12"),

            // ---------------- 华为 / 荣耀 ----------------
            new("华为", "华为 Mate 60 Pro","ALN-AL00",1216, 2688, "12", "HiSilicon Kirin 9000S",   "2620000", 8, 12, 512, "mate60-pro"),
            new("华为", "华为 Pura 70",   "MNA-AL00", 1220, 2700, "12", "HiSilicon Kirin 9010",    "2300000", 8, 12, 256, "pura70"),
            new("华为", "华为 nova 12",   "FNY-AL10", 1224, 2700, "12", "HiSilicon Kirin 9010E",   "2300000", 8,  8, 256, "nova12"),
            new("华为", "华为 Mate 70 Pro","MRA-AL00", 1216, 2688, "13", "HiSilicon Kirin 9020",    "2500000", 8, 12, 512, "mate70-pro"),
            new("华为", "华为 Mate 70 Pro+","MRR-AL00",1216, 2688, "13", "HiSilicon Kirin 9020",    "2500000", 8, 16, 512, "mate70-pro-plus"),
            new("华为", "华为 Pura 80",   "MNA-AL20", 1220, 2700, "15", "HiSilicon Kirin 9020",    "2500000", 8, 12, 256, "pura80"),
            new("华为", "华为 Pura 80 Pro","MNA-AL30",1220, 2700, "15", "HiSilicon Kirin 9020",    "2500000", 8, 16, 512, "pura80-pro"),
            new("华为", "华为 Mate 80 Pro","MRA-AL20",1216, 2688, "16", "HiSilicon Kirin 9030",    "2600000", 8, 16, 512, "mate80-pro"),
            new("华为", "华为 Mate 80 Pro Max","MRR-AL10",1216,2688,"16","HiSilicon Kirin 9030 Pro","2700000", 8, 16, 512, "mate80-pro-max"),
            new("华为", "华为 Mate 90",   "MRA-AL30", 1216, 2688, "17", "HiSilicon Kirin 9030",    "2700000", 8, 12, 256, "mate90"),
            new("华为", "华为 Mate 90 Pro","MRA-AL40",1216, 2688, "17", "HiSilicon Kirin 9030 Pro","2800000", 8, 16, 512, "mate90-pro"),
            new("华为", "华为 Pura X View","MNA-AL40",1220, 2700, "17", "HiSilicon Kirin 9030",    "2700000", 8, 12, 256, "pura-x-view"),
            new("华为", "华为 畅享 100",  "MNA-AL50", 1080, 2400, "17", "HiSilicon Kirin 8000S",   "2200000", 8,  8, 128, "changxiang100"),
            new("华为", "华为 畅享 100 Pro Max","MNA-AL60",1080,2400,"17","HiSilicon Kirin 8020A",  "2300000", 8,  8, 128, "changxiang100-pro-max"),
            new("荣耀", "荣耀 Magic5",    "PGT-AN00", 1264, 2800, "13", "Qualcomm Snapdragon 8 Gen 2","3200000", 8, 12, 256, "honor-magic5"),
            new("荣耀", "荣耀 Magic6",    "BVL-N49",  1264, 2800, "14", "Qualcomm Snapdragon 8 Gen 3","3300000", 8, 12, 256, "honor-magic6"),
            new("荣耀", "荣耀 Magic7",    "PTP-AN00", 1264, 2800, "15", "Qualcomm Snapdragon 8 Elite","4320000", 8, 12, 256, "honor-magic7"),
            new("荣耀", "荣耀 Magic7 Pro","PTP-AN10", 1264, 2800, "15", "Qualcomm Snapdragon 8 Elite","4320000", 8, 16, 512, "honor-magic7-pro"),
            new("荣耀", "荣耀 Magic8 Pro","PTP-AN20", 1264, 2800, "16", "Qualcomm Snapdragon 8 Elite Gen 5","4380000", 8, 16, 512, "honor-magic8-pro"),
            new("荣耀", "荣耀 80 GT",     "GTK-AN00", 1200, 2664, "13", "MediaTek Dimensity 8000",  "2750000", 8, 12, 256, "honor-80-gt"),
            new("荣耀", "荣耀 200 Pro",   "FCP-AN10", 1264, 2800, "14", "Qualcomm Snapdragon 8s Gen 3","3000000", 8, 12, 256, "honor-200-pro"),
            new("荣耀", "荣耀 400 Pro",   "FCP-AN20", 1264, 2800, "15", "Qualcomm Snapdragon 8s Gen 4","3100000", 8, 12, 256, "honor-400-pro"),
            new("荣耀", "荣耀 Magic9",    "PTP-AN30", 1264, 2800, "17", "Qualcomm Snapdragon 8 Elite Gen 6","4600000", 8, 12, 256, "honor-magic9"),
            new("荣耀", "荣耀 Magic9 Pro","PTP-AN40", 1264, 2800, "17", "Qualcomm Snapdragon 8 Elite Gen 6","4600000", 8, 16, 512, "honor-magic9-pro"),
            new("荣耀", "荣耀 X80 Pro Max","REA-AN00",1080, 2400, "17", "MediaTek Dimensity 7400",     "2600000", 8, 12, 256, "honor-x80-pro-max"),

            // ---------------- 其他 ----------------
            new("摩托罗拉","moto X40",     "XT2301-5",1080, 2400, "13", "Qualcomm Snapdragon 8+ Gen 1","3190000", 8, 12, 256, "moto-x40"),
            new("摩托罗拉","moto Edge 50", "XT2401-2",1220, 2712, "14", "MediaTek Dimensity 7300",     "2500000", 8,  8, 256, "moto-edge-50"),
            new("摩托罗拉","moto Edge 60 Pro","XT2505-1",1220,2712,"15","MediaTek Dimensity 8350",     "3350000", 8, 12, 256, "moto-edge-60-pro"),
            new("诺基亚", "诺基亚 X30",    "TA-1543", 1080, 2400, "13", "Qualcomm Snapdragon 480+",    "2200000", 8,  8, 128, "nokia-x30"),
            new("索尼",   "Xperia 1 V",    "XQ-DQ72", 1080, 2520, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "xperia-1-v"),
            new("索尼",   "Xperia 1 VII",  "XQ-DQ82", 1080, 2520, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 12, 256, "xperia-1-vii"),
            new("华硕",   "ROG Phone 6",   "ASUS_AI2205",1080,2448,"13","Qualcomm Snapdragon 8+ Gen 1","3190000", 8, 12, 256, "rog-phone-6"),
            new("华硕",   "ROG Phone 8",   "ASUS_AI2401",1080,2448,"14","Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 16, 256, "rog-phone-8"),
            new("华硕",   "ROG Phone 9",   "ASUS_AI2501",1080,2448,"15","Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "rog-phone-9"),
            new("中兴",   "中兴 Axon 40",  "ZTE A2024H",1080,2400,"13", "MediaTek Dimensity 8100",     "2850000", 8,  8, 256, "zte-axon-40"),
            new("努比亚", "努比亚 Z50 Ultra","NX729J", 1116, 2480, "14", "Qualcomm Snapdragon 8 Gen 2", "3200000", 8, 12, 256, "nubia-z50-ultra"),
            new("努比亚", "努比亚 Z60 Ultra","NX769J", 1260, 2800, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "nubia-z60-ultra"),
            new("努比亚", "努比亚 Z70 Ultra","NX737J", 1260, 2800, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "nubia-z70-ultra"),
            new("魅族",   "魅族 20",       "M2391",   1080, 2400, "14", "MediaTek Dimensity 8100",     "2850000", 8, 12, 128, "meizu-20"),
            new("魅族",   "魅族 21",       "M2481",   1080, 2400, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "meizu-21"),
            new("魅族",   "魅族 22",       "M2581",   1080, 2400, "16", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 12, 256, "meizu-22"),
            new("联想",   "拯救者 Y70",    "XT2363-1",1080, 2400, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 16, 256, "legion-y70"),
            new("黑鲨",   "黑鲨 5 Pro",    "SHARK KTUS-H0",1080,2400,"14","Qualcomm Snapdragon 8 Gen 3","3300000",8,12,256,"blackshark-5-pro"),
            new("红魔",   "红魔 9 Pro",    "NX769J-R",1116, 2480, "14", "Qualcomm Snapdragon 8 Gen 3", "3300000", 8, 12, 256, "redmagic-9-pro"),
            new("红魔",   "红魔 11 Pro",   "NX809J",  1116, 2480, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "redmagic-11-pro"),
            new("红魔",   "红魔 10 Pro",   "NX789J",  1116, 2480, "15", "Qualcomm Snapdragon 8 Elite", "4320000", 8, 16, 512, "redmagic-10-pro"),
            new("华硕",   "ROG Phone 10",  "ASUS_AI2601",1080,2448,"17","Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "rog-phone-10"),
            new("努比亚", "努比亚 Z80 Ultra","NX757J", 1260, 2800, "17", "Qualcomm Snapdragon 8 Elite Gen 5", "4380000", 8, 16, 512, "nubia-z80-ultra"),
        };
    }

    // 机型池条目。屏幕 / SoC / 内存 / 存储必须自洽，否则会被识别成伪造设备。
    internal sealed record DeviceEntry(
        string Brand,
        string Model,
        string ModelCode,
        int Width,
        int Height,
        string OsVersion,
        string Cpu,
        string CpuHz,
        int Cores,
        int RamGb,
        int RomGb,
        string Key)
    {
        // 下拉里显示的文本。
        public string DisplayName => $"Android {OsVersion} · {RamGb}G / {RomGb}G";
    }
}
