using System.Text.Json.Serialization;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityUserSearchByUidRequest))]
    internal partial class EntityUserSearchByUidRequestContext : JsonSerializerContext { }

    internal class EntityUserSearchByUidRequest
    {
        [JsonPropertyName("user_id")]
        public required string UserId { get; set; }
    }

    internal class EntityGatewayUser
    {
        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
