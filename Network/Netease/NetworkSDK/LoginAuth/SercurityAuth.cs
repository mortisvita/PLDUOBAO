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

        // 解析 sauth_json。失败返回 null 而不是抛异常——
        // 诊断路径不应该把整条登录链打断。
        public SAuthJson? GetSAuthJson()
        {
            if (string.IsNullOrWhiteSpace(sauth_json)) return null;
            try
            {
                return JsonSerializer.Deserialize(sauth_json, SercurityAuthContext.Default.SAuthJson);
            }
            catch
            {
                return null;
            }
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

            // 真实上报是序列化后的 JSON 字符串，不是对象。
            public string? aim_info { get; set; }

            public string? client_login_sn { get; set; }
            public string? gas_token { get; set; }
            public string? source_platform { get; set; }
            public string? ip { get; set; }
            public string? userid { get; set; }

            // 真实上报是序列化后的 JSON 字符串，不是对象。
            public string? realname { get; set; }

            public string? timestamp { get; set; }

            // ---- 网易侧会话字段（由 mgbsdk uni_sauth 换取后合并进来） ----
            // 网易 aid（用户唯一编号）。
            public string? aid { get; set; }
            // 网易 OAuth2 访问令牌（JWT）。
            public string? access_token { get; set; }
            // 网易 OAuth2 刷新令牌（JWT）。
            public string? refresh_token { get; set; }
            // chl_sdk_json 里的签名，网易侧校验用。
            public string? sign { get; set; }
            // 网易侧会话时间戳。
            public string? netease_timestamp { get; set; }
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
