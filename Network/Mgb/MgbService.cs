using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Mgb.SDK;
using PLDUOBAO.Util;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PLDUOBAO.Network.Mgb
{
    internal class MgbService(string gameId) : IDisposable
    {
        private readonly HttpClient _sdk = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null,
        });

        public void Dispose() 
        {
            _sdk.Dispose();
            GC.SuppressFinalize(this);
        }

        // deviceId: 为空则随机；MPay 等场景传入渠道 device_id
        // udid: 为空则与 deviceId 相同；可与 deviceId 不同（如 32 位 hex）
        public string GenerateSAuth(string userid, string sdkUid, string sessionId, string timestamp, string appChannel, string loginChannel, string platform = "pc", string? deviceId = null, string? udid = null)
        {
            // 设备标识跨会话固定，且必须与 sa_data 同源，否则每次登录都会被
            // 4399 风控判成新设备（403 / subcode 15 / login abnormally）。
            // 调用方显式传入时（例如 MPay 用渠道下发的 device_id）以其为准。
            DeviceIdentity.Ensure();
            string resolvedDeviceId = !string.IsNullOrEmpty(deviceId)
                ? deviceId
                : DeviceIdentity.Udid.ToUpperInvariant();
            string resolvedUdid = !string.IsNullOrEmpty(udid)
                ? udid
                : DeviceIdentity.Udid;

            // 4399 渠道复用持久化 sn，避免每次登录被当成新会话；
            // 其它渠道（如 MPay/netease）保持每次随机，与其原有行为一致。
            string clientLoginSn = string.Equals(loginChannel, "4399com", StringComparison.OrdinalIgnoreCase)
                ? DeviceIdentity.ClientLoginSn
                : Guid.NewGuid().ToString("N").ToUpper();

            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键点
                WriteIndented = false,
            };
            var context = new MgbSdkCookieContext(options);

            return JsonSerializer.Serialize(new MgbSdkCookie
            {
                AppChannel = appChannel,
                ClientLoginSn = clientLoginSn,
                DeviceId = resolvedDeviceId,
                GameId = gameId,
                LoginChannel = loginChannel,
                SdkVersion = "3.12.2",
                SdkUid = sdkUid,
                SessionId = sessionId,
                Timestamp = timestamp,
                Platform = platform,
                SourcePlatform = platform,
                Udid = resolvedUdid,
                UserId = userid,
                Step = string.Concat(Enumerable.Range(0, 10).Select(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10))),
                Step2 = string.Concat(Enumerable.Range(0, 9).Select(_ => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 10)))
            }, context.MgbSdkCookie);
        }

        // 用渠道 SDK 的 sauth 向网易换取正式会话。
        // 这一步是 pe-authentication 的前置条件：服务端要用这里返回的
        // unisdk_login_json（内含 oauth2 access_token / chl_sdk_json.sign）才能
        // 认账号。只拿渠道原始 sauth 去打 pe-authentication，服务端会因为找不到
        // 会话映射而回 code 2001「客户端版本过低」。
        // cookie: 渠道 SDK 生成的 sauth JSON
        // 网易侧 unisdk_login_json（Base64），失败返回 null
        public async Task<string?> AuthSession(string cookie)
        {
            LogSys.Log($"[MgbService] Request Cookie:{LogRedactor.Redact(cookie)}");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://mgbsdk.matrix.netease.com/x19/sdk/uni_sauth");
            var content = new StringContent(cookie, Encoding.UTF8, "application/json");
            request.Content = content;

            var response = await _sdk.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }

            string body = await response.Content.ReadAsStringAsync();
            LogSys.Log($"[MgbService] AuthSession:{LogRedactor.Redact(body)}");

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // 403 / subcode 15 = 渠道侧风控「login abnormally」，需要短信安全验证。
                // 这类失败必须明确抛给上层，否则静默退回原始 sauth 后 pe-authentication
                // 只会回 code 32「服务器维护中」，把真实原因盖住。
                if (root.TryGetProperty("code", out var c)
                    && c.ValueKind == JsonValueKind.Number
                    && c.GetInt32() != 200)
                {
                    string msg = root.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString() ?? string.Empty
                        : string.Empty;
                    int subcode = root.TryGetProperty("subcode", out var sc) && sc.ValueKind == JsonValueKind.Number
                        ? sc.GetInt32() : 0;

                    LogSys.Warn($"[MgbService] AuthSession failed: {LogRedactor.Redact(body)}");
                    throw new InvalidOperationException(
                        $"渠道会话交换失败（code={c.GetInt32()}, subcode={subcode}）：" +
                        (string.IsNullOrWhiteSpace(msg) ? "请稍后重试" : msg));
                }

                if (root.TryGetProperty("unisdk_login_json", out var u)
                    && u.ValueKind == JsonValueKind.String)
                {
                    string? val = u.GetString();
                    if (!string.IsNullOrWhiteSpace(val))
                        return val;
                }
            }
            catch (JsonException ex)
            {
                // 只吞解析异常；上面的 InvalidOperationException（风控失败）
                // 必须继续向上抛，不能静默退回原始 sauth。
                LogSys.Warn($"[MgbService] AuthSession parse err: {ex.Message}");
            }

            return null;
        }

        // 把 unisdk_login_json（Base64）解成明文 JSON。
        // 内容含 aid / sdkuid / oauth2.access_token / chl_sdk_json.sign 等字段。
        public static string? DecodeUnisdkLoginJson(string? base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            try
            {
                byte[] raw = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                return null;
            }
        }
    }
}
