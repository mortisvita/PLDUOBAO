using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityNetGameRequest))]
    internal partial class EntityNetGameRequestContext : JsonSerializerContext {}
    internal class EntityNetGameRequest
    {
        [JsonPropertyName("version")]
        public required string Version { get; set; }

        [JsonPropertyName("channel_id")]
        public required int ChannelId { get; set; }
    }
}
