using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK.Response
{
    [JsonSerializable(typeof(X4399OAuthResponse))]
    internal partial class X4399OAuthResponseContext : JsonSerializerContext
    {

    }
    internal class X4399OAuthResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("result")]
        public X4399OAuthResult Result { get; set; } = new X4399OAuthResult();
    }
}
