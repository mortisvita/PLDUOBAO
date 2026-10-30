using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK
{
    [JsonSerializable(typeof(PESA_Data))]
    internal partial class PESA_DataContext : JsonSerializerContext
    {

    }
    internal class PESA_Data
    {
         [JsonPropertyName("app_channel")]
         public string? AppChannel { get; set; }

         [JsonPropertyName("app_ver")]
         public string? AppVersion { get; set; }

         [JsonPropertyName("core_num")]
         public string? CoreNum { get; set; }

         [JsonPropertyName("cpu_digit")]
         public string? CpuDigit { get; set; }

         [JsonPropertyName("cpu_hz")]
         public string? CpuHz { get; set; }

         [JsonPropertyName("cpu_name")]
         public string? CpuName { get; set; }

         [JsonPropertyName("device_height")]
         public string? DeviceHeight { get; set; }

         [JsonPropertyName("device_model")]
         public string? DeviceModel { get; set; }

         [JsonPropertyName("device_width")]
         public string? DeviceWidth { get; set; }

         [JsonPropertyName("disk")]
         public string? Disk { get; set; }

         [JsonPropertyName("emulator")]
         public int Emulator { get; set; }

         [JsonPropertyName("first_udid")]
         public string? FirstUdid { get; set; }

         [JsonPropertyName("is_guest")]
         public int IsGuest { get; set; }

         [JsonPropertyName("launcher_type")]
         public string LauncherType { get; set; } = "PE_C++";

         [JsonPropertyName("mac_addr")]
         public string? MacAddress { get; set; }

         [JsonPropertyName("network")]
         public string? Network { get; set; }

         [JsonPropertyName("os_name")]
         public string? OsName { get; set; }

         [JsonPropertyName("os_ver")]
         public string? OsVersion { get; set; }

         [JsonPropertyName("ram")]
         public string? Ram { get; set; }

         [JsonPropertyName("rom")]
         public string? Rom { get; set; }

        [JsonPropertyName("root")]
        public bool Root { get; set; } = false;

         [JsonPropertyName("sdk_ver")]
         public string? SdkVersion { get; set; }

         [JsonPropertyName("start_type")]
         public string? StartType { get; set; }

         [JsonPropertyName("udid")]
         public string? Udid { get; set; }
    }
}
