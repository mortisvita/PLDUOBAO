using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PLDUOBAO.Util;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK
{
    // 生成 pe-authentication 的 sa_data。
    // 所有设备字段都取自 DeviceProfile.Current，与
    // PlatformPatcher 改写的客户端上报标识保持同一份值。
    // 网络服会把两处交叉比对，任何一处不一致都会被判成伪造设备，
    // 后果就是自定义模型不下发（客户端表现为紫黑方块）。
    internal class PESADataGenerator
    {
        // app_channel：必须与登录渠道一致。
        // 4399 渠道走 allysdk.4399com（与 MgbService.GenerateSAuth 传入的 appChannel 相同）。
        private const string AppChannel = "allysdk.4399com";

        // app_ver：客户端实际运行版本。真实 4399 客户端上报的是补丁版本
        // （内存抓包实测 app_ver = 3.10.12.301658，与 patch_version 相同）。
        private static string AppVersion => VerificationProfile.PatchVersion;

        // sdk_ver：内嵌 unisdk 版本。真实 4399 客户端上报 3.18.0（内存抓包实测）。
        private const string SdkVer = "3.18.0";

        public static string GenerateSaDataPE()
        {
            var dev = DeviceProfile.Current;

            var obj = new PESA_Data
            {
                AppChannel = AppChannel,
                AppVersion = AppVersion,
                CoreNum = dev.CoreNum.ToString(),
                CpuDigit = "64",
                CpuHz = dev.CpuHz,
                CpuName = dev.CpuName,
                DeviceHeight = dev.DeviceHeight.ToString(),
                DeviceWidth = dev.DeviceWidth.ToString(),
                // sa_data 上报厂商内部型号代码（例如 2211133C），不是中文设备名。
                // 中文名只用于界面显示。
                DeviceModel = dev.ModelCode,
                Disk = "",
                Emulator = 0,
                FirstUdid = dev.Udid,
                IsGuest = 0,
                LauncherType = "PE_C++",
                MacAddress = dev.MacAddress,
                Network = "CHANNEL_UNKNOW",
                OsName = dev.OsName,
                OsVersion = dev.OsVersion,
                Ram = dev.RamBytes.ToString(),
                Rom = dev.RomBytes.ToString(),

                // 真实 4399 客户端在 root 过的设备上上报 true（内存抓包实测）。
                // 固定 false 反而与其它字段的环境特征不一致。
                Root = false,

                SdkVersion = SdkVer,
                StartType = "default",
                Tdid = "",
                Udid = dev.Udid
            };

            // 真实客户端序列化后在末尾带一个换行符，服务端按原文校验，必须一致
            return JsonSerializer.Serialize(obj, PESA_DataContext.Default.PESA_Data) + "\n";
        }
    }
}
