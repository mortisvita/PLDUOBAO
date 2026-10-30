using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Com4399.SDK.Response;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PCCommon
{
    [JsonSerializable(typeof(AuthenticationUpdate))]
    internal partial class AuthenticationUpdateContext : JsonSerializerContext
    {

    }
    internal class AuthenticationUpdate
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("is_register")]
        public bool IsRegister { get; set; } = true;

        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }
    }
}
