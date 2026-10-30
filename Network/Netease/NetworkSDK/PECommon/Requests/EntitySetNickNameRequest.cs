using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntitySetNickNameRequest))]
    internal partial class EntitySetNickNameRequestContext : JsonSerializerContext{ }
    internal class EntitySetNickNameRequest
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("entity_id")]
        public required string EntityId { get; set; }
    }
}
