using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityNetGameServerAddressRequest))]
    internal partial class EntityNetGameServerAddressRequestContext : JsonSerializerContext { }
    internal class EntityNetGameServerAddressRequest
    {
        [JsonPropertyName("item_id")]
        public required string ItemId { get; set; }
    }
}
