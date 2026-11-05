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

        /// <param name="deviceId">为空则随机；MPay 等场景传入渠道 device_id</param>
        /// <param name="udid">为空则与 deviceId 相同；可与 deviceId 不同（如 32 位 hex）</param>
        public string GenerateSAuth(string userid, string sdkUid, string sessionId, string timestamp, string appChannel, string loginChannel, string platform = "pc", string? deviceId = null, string? udid = null)
        {
            string resolvedDeviceId = string.IsNullOrEmpty(deviceId)
                ? Guid.NewGuid().ToString("N").ToUpper()
                : deviceId;
            string resolvedUdid = string.IsNullOrEmpty(udid) ? resolvedDeviceId : udid;

            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键点
                WriteIndented = false,
            };
            var context = new MgbSdkCookieContext(options);

            return JsonSerializer.Serialize(new MgbSdkCookie
            {
                AppChannel = appChannel,
                ClientLoginSn = Guid.NewGuid().ToString("N").ToUpper(),
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

        public async Task AuthSession(string cookie)
        {
            LogSys.Log($"[MgbService] Request Cookie:{cookie}");

            var request = new HttpRequestMessage(HttpMethod.Post, "https://mgbsdk.matrix.netease.com/x19/sdk/uni_sauth");
            var content = new StringContent(cookie, Encoding.UTF8, "application/json");
            request.Content = content;

            var response = await _sdk.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(response.ReasonPhrase);
            }


            LogSys.Log($"[MgbService] AuthSession:{await response.Content.ReadAsStringAsync()}");

            //StringContent content = new StringContent(cookie, Encoding.UTF8, "application/json");
            //HttpResponseMessage httpResponseMessage = await _sdk.PostAsync("https://mgbsdk.matrix.netease.com/x19/sdk/uni_sauth", content);
            ////httpResponseMessage.Headers.Add("User-Agent", "WPFLauncher/0.0.0.0");
            //LogSys.Log($"[MgbService] Request Cookie: {cookie}");
            //LogSys.Log($"[MgbService] Request Uri : {"/" + gameId + "/sdk/uni_sauth"}");
            //if (!httpResponseMessage.IsSuccessStatusCode)
            //{
            //    throw new HttpRequestException(httpResponseMessage.ReasonPhrase);
            //}
            //LogSys.Log($"[MgbService] AuthSession:{await httpResponseMessage.Content.ReadAsStringAsync()}");
            //Dictionary<string, object> dictionary = JsonSerializer.Deserialize<Dictionary<string, object>>(await httpResponseMessage.Content.ReadAsStringAsync());
            //if (dictionary["code"].ToString() != "200")
            //{
            //    throw new HttpRequestException("Status: " + dictionary["status"]);
            //}
        }
    }
}
