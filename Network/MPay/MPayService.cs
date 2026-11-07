using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.HTTP;
using PLDUOBAO.Network.MPay.SDK;
using PLDUOBAO.Network.MPay.SDK.Response;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Network.Netease.NetworkSDK;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth;
using PLDUOBAO.Network.Mgb;
using PLDUOBAO.Util;

namespace PLDUOBAO.Network.MPay
{
    internal class MPayService
    {
        private MPayDevice? _device;

        private string? _unique;

        private string GameId { get; }

        public string GameVersion { get; }

        public static string LocalMPayPath => PathUtils.CdsStorePath;

        public MPayDevice GetDevice() => this._device!;

        private static async Task<string?> GetMPayLocalFileAsync(string Path)
        {
            if (!File.Exists(Path))
                return null;

            return await File.ReadAllTextAsync(Path);
        }

        private MPayService(string gameId, string gameVersion)
        {
            GameId = gameId;
            GameVersion = gameVersion;
        }

        // 公共异步工厂方法，替代构造函数执行异步初始化
        public static async Task<MPayService> CreateAsync(string gameId, string gameVersion)
        {
            var service = new MPayService(gameId, gameVersion);
            service._device = await service.CreateOrLoadDeviceAsync(gameId, gameVersion);
            service._unique = await CreateOrLoadUniqueAsync(gameId);
            return service;
        }

        private async Task<MPayDevice> CreateOrLoadDeviceAsync(string gameId, string gameVersion)
        {
            string text = gameId + "-device.cds";
            string? result = await GetMPayLocalFileAsync(Path.Combine(LocalMPayPath, text));
            if (result == null)
            {
                return await CreateDeviceAsync(gameId, gameVersion, text);
            }

            try
            {
                var returnVal = JsonSerializer.Deserialize(result, MPayDeviceResponseContext.Default.MPayDeviceResponse);
                if (returnVal == null)
                    throw new JsonException("Prasing json error!");
                return returnVal.EntityDevice;
            }
            catch (Exception ex)
            {
                LogSys.Err($"[MPayService] Failed on prasing json : {ex} ");
                throw;
            }
        }

        public async Task<MPayUserResponse?> LoginWithEmailAsync(string email, string password)
        {
            string value = JsonSerializer.Serialize(new MPayUsersParameters
            {
                Password = password.EncodeMd5(),
                Unique = _unique!,
                Username = email
            },
            MPayUsersParametersContext.Default.MPayUsersParameters)
                .EncodeAes(_device!.Key.DecodeHex())
                .EncodeHex();


            HttpResponseMessage response = await MPayPostAsync_Msg($"/mpay/games/{GameId}/devices/{_device.Id}/users", 
                BuildBaseParams().
                Append("opt_fields", "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status").
                Append("params", value).Append("un", email.EncodeBase64())
                .FormUrlEncode());

            string text = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                MPayVerifyResponse? entityVerifyResponse = JsonSerializer.Deserialize(text,MPayVerifyResponseContext.Default.MPayVerifyResponse);
                if (entityVerifyResponse == null )
                {
                    return null;
                }

                if (entityVerifyResponse.Code == 1351)
                    throw new VerifyException($"账号存在异常登录风险：{entityVerifyResponse.VerifyUrl}",entityVerifyResponse.VerifyUrl);
                
                throw new Exception($"邮箱登录失败: {entityVerifyResponse.Reason} 状态码：{entityVerifyResponse.Code}");
            }
            return JsonSerializer.Deserialize(text,MPayUserResponseContext.Default.MPayUserResponse);
        }

        private async Task<HttpResponseMessage> MPayPostAsync_Msg(string url, string body, int timeoutSeconds = 5)
        {
            LogSys.Log($"[MPayService] Request URL: {url}");
            LogSys.Log($"[MPayService] Request Body: {body}");

            string baseUrl = "https://service.mkey.163.com";
            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"),
            };

            LogSys.Log($"[MPayService] Full Request URL: {request.RequestUri}");

            if (timeoutSeconds <= 0)
                return await NetworkLocalAPI.client.SendAsync(request);

            using var cts = new CancellationTokenSource();
            var sendTask = NetworkLocalAPI.client.SendAsync(request, cts.Token);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

            var completed = await Task.WhenAny(sendTask, timeoutTask);

            if (completed == timeoutTask)
            {
                LogSys.Log("[MPayService] 请求卡死，主动超时退出");
                cts.Cancel();
                throw new TimeoutException($"POST 请求卡住，超过{timeoutSeconds}秒未响应");
            }

            return await sendTask;
        }

        // 扫码 API 使用的 game_id（与设备 game 不同，与遗留 MPay.cs 一致）
        private const string QrMpayGameId = "aecglf6ee4aaaarz-g-a50";

        public enum QrLoginPollKind { Waiting, Completed, Failed }

        public readonly record struct QrLoginPollResult(
            QrLoginPollKind Kind,
            int NextQueryStatus,
            string? Code,
            string? LoginChannel,
            string? Error);

        private static async Task<string> MpayGetStringAsync(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var resp = await NetworkLocalAPI.client.SendAsync(req, ct);
            return await resp.Content.ReadAsStringAsync(ct);
        }

        private static async Task<byte[]> MpayGetImagePngAsync(string url, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.ParseAdd("image/png");
            req.Headers.Accept.ParseAdd("application/json");
            var resp = await NetworkLocalAPI.client.SendAsync(req, ct);
            var media = resp.Content.Headers.ContentType?.MediaType;
            if (media != "image/png")
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"二维码图片非 PNG: {media} / {err}");
            }
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }

        // 创建登录二维码（与 Network/MPay/MPay.cs 中 GetQRCode 同源）
        public async Task<(string Uuid, byte[] PngBytes)> CreateQrLoginPngAsync(CancellationToken ct = default)
        {
            string deviceId = _device!.Id;
            string createUrl =
                "https://service.mkey.163.com/mpay/api/qrcode/create_login" +
                "?app_channel=a50_sdk_cn&qrcode_channel_type=2&dst_jf_game_id=x19&cv=c0.0.0&is_remember=2" +
                $"&device_id={Uri.EscapeDataString(deviceId)}&game_id={Uri.EscapeDataString(QrMpayGameId)}";

            LogSys.Log($"[MPayService][QR] create: {createUrl}");
            string createResponse = await MpayGetStringAsync(createUrl, ct);
            var created = JsonSerializer.Deserialize(createResponse,
                MPayQRCreateResponseContext.Default.MPayQRCreateResponse);
            if (created == null)
                throw new InvalidOperationException("创建二维码：无法解析响应");

            if (created.Code != null && created.Code != 0)
                throw new InvalidOperationException($"创建二维码失败: {created.Reason ?? created.Code.ToString()}");

            if (string.IsNullOrEmpty(created.Uuid))
                throw new InvalidOperationException("创建二维码响应缺少 uuid");

            string imageUrl =
                "https://service.mkey.163.com/mpay/api/qrcode/image" +
                $"?uuid={Uri.EscapeDataString(created.Uuid)}&game_id={Uri.EscapeDataString(QrMpayGameId)}&cv=c0.0.0";

            byte[] png = await MpayGetImagePngAsync(imageUrl, ct);
            return (created.Uuid, png);
        }

        public async Task<QrLoginPollResult> PollQrLoginAsync(string uuid, int queryStatus, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(uuid))
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "uuid 为空");

            string url;
            if (queryStatus == 2)
            {
                url = "https://service.mkey.163.com/mpay/api/qrcode/query" +
                      $"?cv=c0.0.0&game_id={Uri.EscapeDataString(QrMpayGameId)}&uuid={Uri.EscapeDataString(uuid)}";
            }
            else if (queryStatus is 0 or 1)
            {
                url = "https://service.mkey.163.com/mpay/api/qrcode/query" +
                      $"?curr_status={queryStatus}&cv=c0.0.0&game_id={Uri.EscapeDataString(QrMpayGameId)}&uuid={Uri.EscapeDataString(uuid)}";
            }
            else
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "status 参数非法");

            LogSys.Log($"[MPayService][QR] poll: {url}");
            string response = await MpayGetStringAsync(url, ct);
            if (string.IsNullOrEmpty(response))
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "轮询无响应");

            MPayQRQueryResponse? doc;
            try
            {
                doc = JsonSerializer.Deserialize(response,
                    MPayQRQueryResponseContext.Default.MPayQRQueryResponse);
            }
            catch (Exception ex)
            {
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "解析轮询 JSON 失败: " + ex.Message);
            }

            if (doc == null)
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "轮询响应为空");

            if (doc.Code != null && doc.Code != 0)
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, doc.Reason ?? $"code {doc.Code}");

            if (doc.QrCode == null)
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "缺少 qrcode 字段");

            int qrcodeStatus = doc.QrCode.Status;

            if (qrcodeStatus is 0 or 1)
                return new QrLoginPollResult(QrLoginPollKind.Waiting, qrcodeStatus, null, null, null);

            if (qrcodeStatus != 2)
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, $"未知二维码状态: {qrcodeStatus}");

            if (doc.LoginInfo == null || string.IsNullOrEmpty(doc.LoginInfo.Code))
                return new QrLoginPollResult(QrLoginPollKind.Failed, queryStatus, null, null, "已确认但缺少 login_info.code");

            string channel = string.IsNullOrEmpty(doc.LoginInfo.LoginChannel) ? "netease" : doc.LoginInfo.LoginChannel;
            return new QrLoginPollResult(QrLoginPollKind.Completed, 2, doc.LoginInfo.Code, channel, null);
        }

        private async Task<string> ResolveQrTicketAsync(string code, CancellationToken ct)
        {
            string deviceId = _device!.Id;
            string ticketUrl =
                "https://loadingbaycn.webapp.163.com/app/v1/third_party_account/ticket" +
                $"?code={Uri.EscapeDataString(code)}&app_id=81&device_id={Uri.EscapeDataString(deviceId)}";

            LogSys.Log($"[MPayService][QR] ticket: {ticketUrl}");
            string ticketResponse = await MpayGetStringAsync(ticketUrl, ct);

            var ticketDoc = JsonSerializer.Deserialize(ticketResponse,
                MPayTicketResponseContext.Default.MPayTicketResponse);
            if (ticketDoc == null)
                throw new InvalidOperationException("ticket 响应无效");

            if (ticketDoc.Code != 200)
                throw new InvalidOperationException(ticketDoc.Msg ?? $"ticket code {ticketDoc.Code}");

            if (string.IsNullOrEmpty(ticketDoc.Data?.Ticket))
                throw new InvalidOperationException("ticket 为空");

            return ticketDoc.Data.Ticket;
        }

        // 扫码完成后换 SAUTH JSON 字符串（可直接 PEAuth.Login）
        public async Task<string> CompleteQrLoginAsync(string code, string loginChannel, CancellationToken ct = default)
        {
            if (string.Equals(loginChannel, "netease", StringComparison.OrdinalIgnoreCase))
                return await CompleteQrNeteaseAsync(code, ct);

            return await CompleteQrOtherChannelAsync(code, ct);
        }

        private async Task<string> CompleteQrNeteaseAsync(string code, CancellationToken ct)
        {
            string ticket = await ResolveQrTicketAsync(code, ct);
            string deviceId = _device!.Id;
            string mcountTransactionId = Guid.NewGuid().ToString();
            int processId = Random.Shared.Next(100, 65536);
            string transidUuid = StringGenerator.GenerateHexString(16).Replace("-", "").ToLowerInvariant();
            string transid = transidUuid + "_1767196800000_100000000";
            string uniTransactionId = transid;

            var body = new ParameterBuilder()
                .Append("app_channel", "a50_sdk_cn")
                .Append("app_mode", "2")
                .Append("app_type", "games")
                .Append("arch", "win_x64")
                .Append("cv", "c4.16.0")
                .Append("device_id", deviceId)
                .Append("game_id", GameId)
                .Append("gv", GameVersion)
                .Append("is_remember_account", "0")
                .Append("jf_game_id", "x19")
                .Append("mcount_app_key", "EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq")
                .Append("mcount_transaction_id", mcountTransactionId)
                .Append("opt_fields", "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status")
                .Append("process_id", processId.ToString())
                .Append("sv", "10.0.26200")
                .Append("ticket", ticket)
                .Append("transid", transid)
                .Append("uni_transaction_id", uniTransactionId)
                .Append("updater_cv", "c1.0.0")
                .FormUrlEncode();

            var resp = await MPayPostAsync_Msg("/mpay/api/users/login/ticket", body, timeoutSeconds: 0);
            string loginResponse = await resp.Content.ReadAsStringAsync(ct);
            LogSys.Log($"[MPayService][QR] ticket login: {loginResponse}");

            using var doc = JsonDocument.Parse(loginResponse);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out _))
            {
                string reason = root.TryGetProperty("reason", out var r) ? (r.GetString() ?? "") : "";
                throw new InvalidOperationException(reason.Length > 0 ? reason : "ticket 登录失败");
            }

            if (!root.TryGetProperty("user", out var userEl))
                throw new InvalidOperationException("响应缺少 user");

            string? token = userEl.TryGetProperty("token", out var t) ? t.GetString() : null;
            string? userId = userEl.TryGetProperty("id", out var id) ? id.GetString() : null;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
                throw new InvalidOperationException("user.token / user.id 缺失");

            string? clientUsername = userEl.TryGetProperty("client_username", out var cu) ? cu.GetString() : null;
            string? displayUsername = userEl.TryGetProperty("display_username", out var du) ? du.GetString() : null;
            string useridForSauth = !string.IsNullOrEmpty(clientUsername)
                ? clientUsername
                : (!string.IsNullOrEmpty(displayUsername) ? displayUsername : userId);

            string udidHex = StringGenerator.GenerateHexString(16).Replace("-", "").ToLowerInvariant();
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string innerJson;
            using (var mgb = new MgbService("x19"))
            {
                innerJson = mgb.GenerateSAuth(
                    useridForSauth.ToLowerInvariant(),
                    userId,
                    token,
                    timestamp,
                    "netease",
                    "netease",
                    "pc",
                    deviceId,
                    udidHex);
            }
            var wrap = new SercurityAuth { sauth_json = innerJson };
            var opt = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            var ctx = new SercurityAuthContext(opt);
            return JsonSerializer.Serialize(wrap, ctx.SercurityAuth);
        }

        private async Task<string> CompleteQrOtherChannelAsync(string code, CancellationToken ct)
        {
            string ticket = await ResolveQrTicketAsync(code, ct);
            string deviceId = _device!.Id;
            byte[] aesKey = _device!.Key.DecodeHex();
            string encryptCode = ticket.EncodeAes(aesKey).EncodeHex();

            string mcountTransactionId = "1";
            int processId = Random.Shared.Next(10000, 65536);
            string transidUuid = StringGenerator.GenerateHexString(16).Replace("-", "").ToLowerInvariant();
            string transid = transidUuid + "_1767196800000_100000000";

            var body = new ParameterBuilder()
                .Append("app_channel", "a50_sdk_cn")
                .Append("app_mode", "2")
                .Append("app_type", "games")
                .Append("arch", "win_x64")
                .Append("cv", "c4.16.0")
                .Append("device_id", deviceId)
                .Append("encrypt_code", encryptCode)
                .Append("game_id", GameId)
                .Append("gv", GameVersion)
                .Append("jf_game_id", "x19")
                .Append("mcount_app_key", "EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq")
                .Append("mcount_transaction_id", mcountTransactionId)
                .Append("opt_fields", "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status")
                .Append("process_id", processId.ToString())
                .Append("sv", "10.0.26200")
                .Append("transid", transid)
                .Append("uni_transaction_id", transid)
                .Append("updater_cv", "c1.0.0")
                .FormUrlEncode();

            var resp = await MPayPostAsync_Msg("/mpay/api/users/login/qrcode/exchange_token", body, timeoutSeconds: 0);
            string exchangeResponse = await resp.Content.ReadAsStringAsync(ct);
            LogSys.Log($"[MPayService][QR] exchange: {exchangeResponse}");

            string? sauthB64 = TryExtractSauthJsonBase64(exchangeResponse);
            if (string.IsNullOrEmpty(sauthB64))
                throw new InvalidOperationException("exchange 响应中未找到 SAUTH_JSON");

            sauthB64 = Uri.UnescapeDataString(sauthB64);
            byte[] sauthBytes = Convert.FromBase64String(sauthB64);
            string sauthInner = Encoding.UTF8.GetString(sauthBytes);
            JsonDocument.Parse(sauthInner); // validate

            var wrap = new SercurityAuth { sauth_json = sauthInner };
            var opt = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = false
            };
            var ctx = new SercurityAuthContext(opt);
            return JsonSerializer.Serialize(wrap, ctx.SercurityAuth);
        }

        private static string? TryExtractSauthJsonBase64(string exchangeResponse)
        {
            using var doc = JsonDocument.Parse(exchangeResponse);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out _))
                return null;

            if (root.TryGetProperty("user", out var user))
            {
                if (user.TryGetProperty("pc_ext_info", out var pc) &&
                    pc.TryGetProperty("extra_unisdk_data", out var extra))
                {
                    var extraStr = extra.GetString();
                    if (!string.IsNullOrEmpty(extraStr))
                    {
                        using var ed = JsonDocument.Parse(extraStr);
                        if (ed.RootElement.TryGetProperty("SAUTH_JSON", out var sj))
                            return sj.GetString();
                    }
                }
            }

            if (root.TryGetProperty("ext_info", out var ext) &&
                ext.TryGetProperty("extra_unisdk_data", out var extra2))
            {
                var extraStr = extra2.GetString();
                if (!string.IsNullOrEmpty(extraStr))
                {
                    using var ed = JsonDocument.Parse(extraStr);
                    if (ed.RootElement.TryGetProperty("SAUTH_JSON", out var sj))
                        return sj.GetString();
                }
            }

            return null;
        }

        private async Task<string> MPayPostAsync(string url,string body)
        {
            var result = await MPayPostAsync_Msg(url,body);

            result.EnsureSuccessStatusCode();

            string text = await result.Content.ReadAsStringAsync();

            return text;
        }

        private async Task<MPayDevice> CreateDeviceAsync(string gameId, string gameVersion, string fileName)
        {     
            string text = await MPayPostAsync("/mpay/games/" + gameId + "/devices", BuildDeviceParams().Append("unique_id", _unique!).FormUrlEncode());

            await File.WriteAllTextAsync(Path.Combine(LocalMPayPath, fileName), text);

            try
            {
                var result = JsonSerializer.Deserialize(text, MPayDeviceResponseContext.Default.MPayDeviceResponse);

                if (result == null)
                    throw new JsonException("Json prase failed.");
                
                return result.EntityDevice;
            }
            catch (Exception ex)
            {
                LogSys.Err($"[MPayService] Failed on prasing device Json. {ex}");
                throw;
            }
        }

        private static async Task<string> CreateOrLoadUniqueAsync(string gameId)
        {
            string text = gameId + "-guid.cds";
            string? result = await GetMPayLocalFileAsync(Path.Combine(LocalMPayPath,text));
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
            return await CreateUniqueAsync(text);
        }

        private static async Task<string> CreateUniqueAsync(string fileName)
        {
            string text = Guid.NewGuid().ToString().Replace("-", "");
            await File.WriteAllTextAsync(Path.Combine(LocalMPayPath,fileName), text);
            return text;
        }

        private ParameterBuilder BuildBaseParams()
        {
            return new ParameterBuilder().Append("app_channel", "netease").Append("app_mode", "2").Append("app_type", "games")
                .Append("arch", "win_x64")
                .Append("cv", "c4.2.0")
                .Append("mcount_app_key", "EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq")
                .Append("mcount_transaction_id", "0")
                .Append("process_id", $"{Environment.ProcessId}")
                .Append("sv", "10.0.22621")
                .Append("updater_cv", "c1.0.0")
                .Append("game_id", GameId)
                .Append("gv", GameVersion);
        }

        private ParameterBuilder BuildDeviceParams()
        {
            return BuildBaseParams().Append("brand", "Microsoft").Append("device_model", "pc_mode").Append("device_name", "PC-" + StringGenerator.GenerateRandomString(12))
                .Append("device_type", "Computer")
                .Append("init_urs_device", "0")
                .Append("mac", StringGenerator.GenerateRandomMacAddress())
                .Append("resolution", "1920x1080")
                .Append("system_name", "windows")
                .Append("system_version", "10.0.22621");
        }
    }
}
