using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Com4399.SDK.Response;

namespace PLDUOBAO.Network.Mgb.SDK
{
    [JsonSerializable(typeof(MgbSdkCookie))]
    internal partial class MgbSdkCookieContext : JsonSerializerContext
    {

    }
    internal class MgbSdkCookie
    {
        [JsonPropertyName("aim_info")]
        public string AimInfo { get; set; } = "{\"aim\":\"127.0.0.1\",\"country\":\"CN\",\"tz\":\"+0800\",\"tzid\":\"\"}";

        [JsonPropertyName("app_channel")]
        public required string AppChannel { get; set; }

        [JsonPropertyName("client_login_sn")]
        public required string ClientLoginSn { get; set; }

        [JsonPropertyName("deviceid")]
        public required string DeviceId { get; set; }

        [JsonPropertyName("gameid")]
        public required string GameId { get; set; }

        [JsonPropertyName("gas_token")]
        public string GasToken { get; set; } = string.Empty;

        [JsonPropertyName("ip")]
        public string Ip { get; set; } = "127.0.0.1";

        [JsonPropertyName("login_channel")]
        public required string LoginChannel { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = "pc";

        [JsonPropertyName("realname")]
        public string RealName { get; set; } = "{\"realname_type\":\"0\"}";

        [JsonPropertyName("sdk_version")]
        public string SdkVersion { get; set; } = "1.0.0";

        [JsonPropertyName("sdkuid")]
        public required string SdkUid { get; set; }

        [JsonPropertyName("sessionid")]
        public required string SessionId { get; set; }

        [JsonPropertyName("source_platform")]
        public string SourcePlatform { get; set; } = "pc";

        [JsonPropertyName("timestamp")]
        public required string Timestamp { get; set; }

        [JsonPropertyName("udid")]
        public required string Udid { get; set; }

        [JsonPropertyName("userid")]
        public required string UserId { get; set; }

        [JsonPropertyName("step")]
        public required string Step { get; set; }

        [JsonPropertyName("step2")]
        public required string Step2 { get; set; }

        [JsonPropertyName("hostid")] public int HostId { get; set; } = 0;

        [JsonPropertyName("get_access_token")] public string GetAccessToken { get; set; } = "1";

        [JsonPropertyName("is_unisdk_guest")]
        public int IsUniSDKGuest { get; set; } = 0;
    }
}
