using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Util
{
    internal class RandomDeviceSpoof
    {
        private static readonly List<string> iOSDevices = new List<string> {
        "iPhone5,3", "iPhone5,4", "iPhone6,1", "iPhone6,2", "iPhone7,2", "iPhone7,1",
        "iPhone8,1", "iPhone8,2", "iPhone8,4", "iPhone9,1", "iPhone9,3", "iPhone9,2",
        "iPhone9,4", "iPhone10,1", "iPhone10,4", "iPhone10,2", "iPhone10,5", "iPhone10,3",
        "iPhone10,6", "iPhone11,8", "iPhone11,2", "iPhone11,6", "iPhone12,1", "iPhone12,3",
        "iPhone12,5", "iPhone12,8", "iPhone13,1", "iPhone13,2", "iPhone13,3", "iPhone13,4",
        "iPhone14,4", "iPhone14,5", "iPhone14,2", "iPhone14,3", "iPhone14,6", "iPhone14,7",
        "iPhone14,8", "iPhone15,2", "iPhone15,3", "iPhone15,4", "iPhone15,5", "iPhone16,1",
        "iPhone16,2", "iPad6,11", "iPad6,12", "iPad7,5", "iPad7,6", "iPad7,11", "iPad7,12",
        "iPad11,6", "iPad11,7", "iPad12,1", "iPad12,2", "iPad13,18", "iPad13,19", "iPad4,4",
        "iPad4,5", "iPad4,6", "iPad4,7", "iPad4,8", "iPad4,9", "iPad5,1", "iPad5,2", "iPad11,1",
        "iPad11,2", "iPad14,1", "iPad14,2", "iPad4,1", "iPad4,2", "iPad4,3", "iPad5,3", "iPad5,4",
        "iPad11,3", "iPad11,4", "iPad13,1", "iPad13,2", "iPad13,16", "iPad13,17", "iPad6,3", "iPad6,4",
        "iPad7,3", "iPad7,4", "iPad8,1", "iPad8,2", "iPad8,3", "iPad8,4", "iPad8,9", "iPad8,10", "iPad13,4",
        "iPad13,5", "iPad13,6", "iPad13,7", "iPad14,3", "iPad14,4", "iPad6,7", "iPad6,8", "iPad7,1", "iPad7,2",
        "iPad8,5", "iPad8,6", "iPad8,7", "iPad8,8", "iPad8,11", "iPad8,12", "iPad13,8", "iPad13,9", "iPad13,10",
        "iPad13,11", "iPad14,5", "iPad14,6"
        };

        public static string GetRandomDevice()
        {
            int index = Random.Shared.Next(iOSDevices.Count);
            return iOSDevices[index];
        }
    }
}
