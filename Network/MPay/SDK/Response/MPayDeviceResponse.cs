using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests;

namespace PLDUOBAO.Network.MPay.SDK.Response
{
    [JsonSerializable(typeof(MPayDeviceResponse))]
    internal partial class MPayDeviceResponseContext : JsonSerializerContext { }
    internal class MPayDeviceResponse
    {
        [JsonPropertyName("device")]
        public MPayDevice EntityDevice { get; set; } = new MPayDevice();
    }
}
