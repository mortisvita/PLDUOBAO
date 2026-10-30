using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon
{
    [JsonSerializable(typeof(NetGame))]
    [JsonSerializable(typeof(NetGameItem))]
    [JsonSerializable(typeof(List<NetGameItem>))]
    internal partial class NetGameContext : JsonSerializerContext
    {

    }
    internal class NetGame
    {
        [JsonPropertyName("res")]
        public required List<NetGameItem> Res { get; set; }

        [JsonPropertyName("tag")]
        public required int Tag { get; set; }

        [JsonPropertyName("campaign_id")]
        public required int CampaignId { get; set; }
    }
}
