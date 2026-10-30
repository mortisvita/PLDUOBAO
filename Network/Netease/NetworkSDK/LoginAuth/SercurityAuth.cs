using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using static PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.SercurityAuth;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth
{
    [JsonSerializable(typeof(SercurityAuth))]
    [JsonSerializable(typeof(SAuthJson))]
    internal partial class SercurityAuthContext : JsonSerializerContext
    {

    }
    internal class SercurityAuth
    {
        public string? sauth_json { get; set; }

        public SAuthJson GetSAuthJson()
        {
            return JsonSerializer.Deserialize(sauth_json, SercurityAuthContext.Default.SAuthJson)!;
        }

        public class SAuthJson
        {
            public string? gameid { get; set; }
            public string? login_channel { get; set; }
            public string? app_channel { get; set; }
            public string? platform { get; set; }
            public string? sdkuid { get; set; }
            public string? sessionid { get; set; }
            public string? sdk_version { get; set; }
            public string? udid { get; set; }
            public string? deviceid { get; set; }
            public AimInfo? aim_info { get; set; }
            public string? client_login_sn { get; set; }
            public string? gas_token { get; set; }
            public string? source_platform { get; set; }
            public string? ip { get; set; }
            public string? userid { get; set; }
            public Realname? realname { get; set; }
            public string? timestamp { get; set; }
        }

        public class AimInfo
        {
            public string? aim { get; set; }
            public string? country { get; set; }
            public string? tz { get; set; }
            public string? tzid { get; set; }
        }

        public class Realname
        {
            public string? realname_type { get; set; }
        }

    }
}
