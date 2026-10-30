using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.Converter;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.Network.Netease.NetworkSDK.PCCommon;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon
{
    [JsonSerializable(typeof(RentalGame))]
    [JsonSerializable(typeof(RentalGameItem))]
    [JsonSerializable(typeof(List<RentalGameItem>))]
    internal partial class RentalGameContext : JsonSerializerContext
    {

    }
    internal class RentalGame
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("details")]
        public string? Details { get; set; }

        [JsonPropertyName("entities")]
        public List<RentalGameItem>? Entities { get; set; }

        [JsonConverter(typeof(IntOrStringConverter))]
        [JsonPropertyName("total")]
        public int? Total { get; set; }
    }
}
