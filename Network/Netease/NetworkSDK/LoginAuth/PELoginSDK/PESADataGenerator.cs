using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK
{
    internal class PESADataGenerator
    {
        private static readonly Random _random = new(DateTime.Now.Millisecond + DateTime.Now.Second + DateTime.Now.Day + DateTime.Now.Minute + DateTime.Now.Hour);

        private static string RandomHex(int length) =>
        Guid.NewGuid().ToString("N")[..length].ToUpper();

        private static string RandomMacAddress() =>
            $"02:00:00:{_random.Next(0x00, 0xFF):X2}:{_random.Next(0x00, 0xFF):X2}:{_random.Next(0x00, 0xFF):X2}";

        private static string RandomDeviceModel() =>
        _random.Next(0, 20) switch
        {
            0 => "HUAWEI MGA-AL00",
            1 => "Redmi Note 11",
            2 => "OPPO CPH2205",
            3 => "Samsung SM-A515F",
            4 => "Xiaomi Mi 11 Lite",
            5 => "realme RMX3085",
            6 => "vivo V2040",
            7 => "HONOR JEF-AN00",
            8 => "OnePlus AC2001",
            9 => "Samsung SM-G9910",
            10 => "Redmi K40",
            11 => "Xiaomi 2201117TG",
            12 => "OPPO A54",
            13 => "realme GT Master",
            14 => "vivo Y76 5G",
            15 => "Samsung Galaxy M33",
            16 => "Motorola Moto G60",
            17 => "Nokia X20",
            18 => "HUAWEI P40 Lite",
            19 => "Redmi 10C",
            _ => "OPPO Reno5"
        };


        private static string RandomCpuName() =>
            _random.Next(0, 2) switch
            {
                0 => "Kirin 810",
                1 => "Qualcomm Snapdragon 720G",
                2 => "MediaTek Dimensity 800U",
                3 => "Exynos 9611",
                4 => "Snapdragon 732G",
                5 => "Dimensity 920",
                6 => "Snapdragon 870",
                7 => "Kirin 985",
                8 => "Snapdragon 695",
                9 => "Dimensity 6100+",
                10 => "Snapdragon 888",
                11 => "Unisoc T610",
                12 => "Exynos 1280",
                13 => "Snapdragon 680",
                14 => "Kirin 9000E",
                15 => "Snapdragon 765G",
                16 => "Dimensity 810",
                17 => "Snapdragon 7 Gen 1",
                18 => "Tensor G1",
                19 => "Snapdragon 4 Gen 1",
                _ => "MediaTek Helio G96"
            };

        private static (int width, int height) RandomResolution() =>
            _random.Next(0, 2) switch
            {
                0 => (1600, 900),
                1 => (2400, 1080),
                2 => (2280, 1080),
                3 => (2340, 1080),
                4 => (2460, 1080),
                5 => (2520, 1080),
                6 => (2408, 1080),
                7 => (2000, 1200),
                8 => (1920, 1080),
                9 => (2220, 1080),
                10 => (1600, 720),
                11 => (1440, 720),
                12 => (2960, 1440),
                13 => (3120, 1440),
                14 => (2160, 1080),
                15 => (2800, 1260),
                16 => (2640, 1200),
                17 => (2560, 1440),
                18 => (2400, 1176),
                19 => (2310, 1080),
                _ => (2240, 1080)
            };

        public static string GenerateSaDataPE()
        {
            var (width, height) = RandomResolution();

            var obj = new PESA_Data
            {
                AppChannel = "netease",
                AppVersion = "3.9.0.296599",
                CoreNum = Environment.ProcessorCount.ToString(),
                CpuDigit = "64",
                CpuHz = _random.Next(1600000, 2800000).ToString(), // 1.6 GHz - 2.8 GHz
                CpuName = RandomCpuName(),
                DeviceHeight = height.ToString(),
                DeviceWidth = width.ToString(),
                DeviceModel = RandomDeviceModel(),
                Disk = "",
                Emulator = 0,
                FirstUdid = RandomHex(16),
                IsGuest = 0,
                LauncherType = "PE_C++",
                MacAddress = RandomMacAddress(),
                Network = "CHANNEL_UNKNOW",
                OsName = "android",
                OsVersion = _random.Next(10, 14).ToString(), // Android 10–13
                Ram = (_random.Next(3, 7) * 1024 * 1024 * 1024L).ToString(), // 3–6 GB
                Rom = (_random.Next(32, 129) * 1024L * 1024L * 1024L).ToString(), // 32–128 GB
                Root = false,
                SdkVersion = "5.2.0",
                StartType = "default",
                Udid = RandomHex(16)
            };

            return JsonSerializer.Serialize(obj,PESA_DataContext.Default.PESA_Data);
        }
    }
}
