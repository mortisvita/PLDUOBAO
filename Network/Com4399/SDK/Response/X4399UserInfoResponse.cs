using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK.Response
{
    [JsonSerializable(typeof(X4399UserInfoResponse))]
    internal partial class X4399UserInfoResponseContext : JsonSerializerContext
    {

    }
    internal class X4399UserInfoResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public X4399UserInfoResult? Result { get; set; } = new X4399UserInfoResult();
    }
}
