using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.MPay.SDK.Response;

namespace PLDUOBAO.Network.MPay.SDK
{
    [JsonSerializable(typeof(MPayDevice))]
    internal partial class MPayDeviceContext : JsonSerializerContext { }
    internal class MPayDevice
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
    }
}
