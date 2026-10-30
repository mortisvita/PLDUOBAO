using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.MPay.SDK;
using PLDUOBAO.Network.MPay.SDK.Response;
using PLDUOBAO.Network.Netease.NetworkSDK.PatchVersion;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Cookie
{
    [JsonSerializable(typeof(X19Cookie))]
    internal partial class X19CookieContext : JsonSerializerContext
    {

    }
    internal class X19Cookie
    {
        [JsonPropertyName("gameid")]
        public string GameId { get; set; } = "x19";

        [JsonPropertyName("login_channel")]
        public string LoginChannel { get; set; } = "netease";

        [JsonPropertyName("app_channel")]
        public string AppChannel { get; set; } = "netease";

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = "pc";

        [JsonPropertyName("sdkuid")]
        public required string SdkUid { get; set; }

        [JsonPropertyName("sessionid")]
        public required string SessionId { get; set; }

        [JsonPropertyName("sdk_version")]
        public string SdkVersion { get; set; } = "4.2.0";

        [JsonPropertyName("udid")]
        public required string Udid { get; set; }

        [JsonPropertyName("deviceid")]
        public required string DeviceId { get; set; }

        [JsonPropertyName("aim_info")]
        public string AimInfo { get; set; } = "{\"aim\":\"127.0.0.1\",\"country\":\"CN\",\"tz\":\"+0800\",\"tzid\":\"\"}";

        
    }
}
