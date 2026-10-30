using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.Converter;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon
{
    public class RentalGameItem
    {
        [JsonPropertyName("entity_id")]
        public string? EntityId { get; set; }

        [JsonPropertyName("name")]
        public string? RoomId { get; set; }  // 房间号

        [JsonPropertyName("owner_id")]
        public long OwnerId { get; set; }

        [JsonPropertyName("visibility")]
        public int Visibility { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("icon_index")]
        public int IconIndex { get; set; }

        [JsonPropertyName("capacity")]
        public int Capacity { get; set; }

        [JsonPropertyName("mc_version")]
        public string? McVersion { get; set; }

        [JsonPropertyName("player_count")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("like_num")]
        public int LikeNum { get; set; }

        [JsonPropertyName("server_type")]
        public string? ServerType { get; set; }

        [JsonPropertyName("offset")]
        public int? Offset { get; set; }

        [JsonPropertyName("has_pwd")]
        public string? HasPassword { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("world_id")]
        public string? WorldId { get; set; }

        [JsonConverter(typeof(IntOrStringConverter))]
        [JsonPropertyName("min_level")]
        public int? MinLevel { get; set; }

        [JsonPropertyName("pvp")]
        public bool Pvp { get; set; }

        [JsonPropertyName("server_name")]
        public string? ServerName { get; set; }
    }
}
