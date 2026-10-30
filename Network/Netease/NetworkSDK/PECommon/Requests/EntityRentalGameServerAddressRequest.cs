using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityRentalGameServerAddressRequest))]
    internal partial class EntityRentalGameServerAddressRequestContext : JsonSerializerContext
    {

    }
    internal class EntityRentalGameServerAddressRequest
    {
        [JsonPropertyName("server_id")]
        public required string ServerId { get; set; }

        [JsonPropertyName("pwd")]
        public required string Password { get; set; }
    }
}
