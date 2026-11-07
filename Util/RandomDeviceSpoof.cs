using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Util
{
    // 供原生 DeviceSpoof 使用的机型字符串。
    // 取值必须与 DeviceProfile 完全一致：原生层注入的 DeviceModel 会和
    // sa_data 里的 device_model 交叉比对，两边不一致同样会被判成伪造设备。
    internal class RandomDeviceSpoof
    {
        // 当前运行的设备型号（单一来源，全进程固定）。
        public static string GetRandomDevice() => DeviceProfile.Current.Model;
    }
}
