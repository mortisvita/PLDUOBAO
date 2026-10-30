using System.Text.Json.Serialization;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityPcNickNameUpdateRequest))]
    internal partial class EntityPcNickNameUpdateRequestContext : JsonSerializerContext { }

    internal class EntityPcNickNameUpdateRequest
    {
        [JsonPropertyName("name")]
        public required string Name { get; set; }
    }

    internal class EntityPcNickNameUpdateEntity
    {
        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("remain_revise_name_cnt")]
        public int RemainReviseNameCnt { get; set; }
    }
}
