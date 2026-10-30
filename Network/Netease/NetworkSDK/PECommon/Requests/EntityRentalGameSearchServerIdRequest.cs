using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests
{
    [JsonSerializable(typeof(EntityRentalGameSearchServerIdRequest))]
    internal partial class EntityRentalGameSearchServerIdRequestContext : JsonSerializerContext { }
    internal class EntityRentalGameSearchServerIdRequest
    {
        [JsonPropertyName("server_name")]
        public required string ServerId { get; set; }

        [JsonPropertyName("offset")]
        public required int Offset { get; set; }
    }
}
