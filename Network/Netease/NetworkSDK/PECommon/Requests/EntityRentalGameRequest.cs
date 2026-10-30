using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.PCCommon;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityRentalGameRequest))]
    internal partial class EntityRentalGameRequestContext : JsonSerializerContext
    {

    }
    internal class EntityRentalGameRequest
    {
        [JsonPropertyName("sort_type")]
        public required int SortType { get; set; }

        [JsonPropertyName("order_type")]
        public required int OrderType { get; set; }

        [JsonPropertyName("offset")]
        public required int Offset { get; set; }
    }
}
