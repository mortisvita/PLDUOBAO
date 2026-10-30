using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.Cookie;

namespace PLDUOBAO.Network.Com4399.SDK
{
    [JsonSerializable(typeof(X4399Device))]
    internal partial class X4399DeviceContext : JsonSerializerContext
    {

    }
    internal class X4399Device
    {
        [JsonPropertyName("device-id")]
        public required string DeviceIdentifier { get; set; }

        [JsonPropertyName("device-id-sm")]
        public required string DeviceIdentifierSm { get; set; }

        [JsonPropertyName("device-udid")]
        public required string DeviceUdid { get; set; }

        [JsonPropertyName("device-state")]
        public string? DeviceState { get; set; }
    }
}
