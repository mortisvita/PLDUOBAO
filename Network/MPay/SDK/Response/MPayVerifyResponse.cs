using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.MPay.SDK.Response
{
    [JsonSerializable(typeof(MPayVerifyResponse))]
    internal partial class MPayVerifyResponseContext : JsonSerializerContext { }
    internal class MPayVerifyResponse
    {
        [JsonPropertyName("reason")]
        public string Reason { get; set; } = string.Empty;

        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("verify_url")]
        public string VerifyUrl { get; set; } = string.Empty;
    }
}
