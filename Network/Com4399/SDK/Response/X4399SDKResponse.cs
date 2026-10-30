using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK.Response
{
    [JsonSerializable(typeof(X4399SDKResponse))]
    internal partial class X4399SDKResponseContext : JsonSerializerContext
    {

    }
    internal class X4399SDKResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string? Message { get; set; }

        [JsonPropertyName("data")]
        public X4399SDKData? Data { get; set; }

        public bool IsValid()
        {
            return Code == 10000 &&
                   !string.IsNullOrEmpty(Message) &&
                   Data != null &&
                   Data.IsValid();
        }
    }

    public class X4399SDKData
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("login_tip")]
        public string? LoginTip { get; set; }

        [JsonPropertyName("sdk_login_data")]
        public string? SdkLoginData { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Username) &&
                   !string.IsNullOrEmpty(LoginTip) &&
                   !string.IsNullOrEmpty(SdkLoginData);
        }
    }
}
