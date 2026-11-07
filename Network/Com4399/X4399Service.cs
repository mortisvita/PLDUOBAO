using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Avalonia.Controls;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Com4399.SDK;
using PLDUOBAO.Network.Com4399.SDK.Exceptions;
using PLDUOBAO.Network.Com4399.SDK.Response;
using PLDUOBAO.Network.HTTP;
using PLDUOBAO.Network.Mgb;
using PLDUOBAO.Network.Mgb.SDK;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth;
using PLDUOBAO.Util;
using Microsoft.VisualBasic;
using PLDUOBAO.Network.Com4399.Captcha;
//using Newtonsoft.Json;
using static System.Net.Mime.MediaTypeNames;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PLDUOBAO.Network.Com4399
{
    internal class X4399Service
    {
        private const string Com4399File = "4399com.cds";

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        // 直连，忽略系统代理：Fiddler/抓包工具退出后残留的死代理会导致 10061，
        // 登录链路必须与调试代理解耦。
        private readonly HttpClient _4399Api = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null,
        })
        {
            BaseAddress = new Uri("https://m.4399api.com/")
        };

        private static readonly CookieContainer loginCookieContainer = new CookieContainer();

        private readonly HttpClient _login =
        new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = loginCookieContainer,
            UseCookies = true,
            UseProxy = false,
            Proxy = null,
        })
        {
            BaseAddress = new Uri("https://ptlogin.4399.com/"),
        };

        private readonly MgbService _mgbSdk = new MgbService("x19");

        private string _deviceIdentifier = string.Empty;

        private string _deviceIdentifierSm = string.Empty;

        private string _state = string.Empty;

        private string _udid = string.Empty;

        public string CurrentCaptchaId { get; set; } = "";

        public static string Local4399Path => PathUtils.CdsStorePath;

        public static async Task<X4399Service> GetServiceAsync()
        {
            var result = new X4399Service();
            await result.CreateOrLoadDevice();
            return result;
        }

        private static async Task<string?> Get4399LocalFileAsync(string Path)
        {
            if (!File.Exists(Path))
                return null;

            return await File.ReadAllTextAsync(Path);
        }

        private async Task CreateOrLoadDevice()
        {
            string? Context = await Get4399LocalFileAsync(Path.Combine(Local4399Path, "4399com.cds"));
            X4399Device? Device = !string.IsNullOrEmpty(Context) ? LoadDevice(Context) : await CreateDevice();

            if (Device == null)
                Device = await CreateDevice();

            if (string.IsNullOrEmpty(Device.DeviceState))
                Device = await CreateDevice();

            _deviceIdentifier = Device.DeviceIdentifier;
            _deviceIdentifierSm = Device.DeviceIdentifierSm;
            _udid = Device.DeviceUdid;
            _state = Device.DeviceState!;
        }

        private static X4399Device? LoadDevice(string? data)
        {
            if (string.IsNullOrEmpty(data))
                return null;

            return JsonSerializer.Deserialize(data,
                X4399DeviceContext.Default.X4399Device);
        }

        private async Task<X4399Device> CreateDevice()
        {
            _deviceIdentifier = GenerateIdentifier();
            _deviceIdentifierSm = GenerateIdentifier();
            _udid = Guid.NewGuid().ToString();
            string deviceState = await OAuthDevice();
            X4399Device entity4399Device = new X4399Device
            {
                DeviceIdentifier = _deviceIdentifier,
                DeviceIdentifierSm = _deviceIdentifierSm,
                DeviceUdid = _udid,
                DeviceState = deviceState
            };

            await File.WriteAllTextAsync(Path.Combine(Local4399Path, "4399com.cds"),
            JsonSerializer.Serialize(entity4399Device, X4399DeviceContext.Default.X4399Device));

            return entity4399Device;
        }

        private async Task<string> OAuthDevice()
        {
            string body = new ParameterBuilder()
                .Append("usernames", "")
                .Append("top_bar", "1")
                .Append("state", "")
                .Append("device", JsonSerializer.Serialize(new X4399OAuth
                {
                    DeviceIdentifier = _deviceIdentifier,
                    DeviceIdentifierSm = _deviceIdentifierSm,
                    Udid = _udid
                }, X4399OAuthContext.Default.X4399OAuth))
                .FormUrlEncode();

            HttpContent content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            HttpResponseMessage response = await _4399Api.PostAsync("/openapiv2/oauth.html", content);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(result);
            if (!doc.RootElement.TryGetProperty("result", out var resultProp))
                throw new Exception($"OAuth device failed (no result): {result}");

            JsonElement resultObj;
            if (resultProp.ValueKind == JsonValueKind.Array && resultProp.GetArrayLength() > 0)
                resultObj = resultProp[0];
            else if (resultProp.ValueKind == JsonValueKind.Object)
                resultObj = resultProp;
            else
                throw new Exception($"OAuth device failed (unexpected result): {result}");

            if (!resultObj.TryGetProperty("login_url", out var loginUrlEl))
                throw new Exception($"OAuth device failed (no login_url): {result}");

            string loginUrl = loginUrlEl.GetString() ?? "";
            if (string.IsNullOrEmpty(loginUrl))
                throw new Exception($"OAuth device failed (empty login_url): {result}");

            string state = new ParameterBuilder(loginUrl).Get("state");
            if (string.IsNullOrEmpty(state))
                throw new Exception($"OAuth device failed (no state in login_url): {loginUrl}");

            return state;
        }

        private async Task<string> RequestStateAsync()
        {
            var (state, _) = await RequestStateAndUrlAsync();
            return state;
        }

        // 取得 oauth state 以及对应的 authorize.do 地址。
        // authorize.do 必须先被访问以建立会话 Cookie（USESSIONID），
        // 否则后续 loginAndAuthorize 会被 WAF 判定为异常请求并返回 202「请稍后再试」。
        private async Task<(string state, string authorizeUrl)> RequestStateAndUrlAsync()
        {
            var response =
                await _4399Api.GetAsync("/openapi/oauth-callback.html?gamekey=44770&game_key=115716");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("result", out var resultEl))
                throw new Exception($"RequestState failed (no result): {content}");

            if (resultEl.ValueKind != JsonValueKind.String)
                throw new Exception($"RequestState failed (result not string): {content}");

            string resultUrl = resultEl.GetString() ?? "";
            if (string.IsNullOrEmpty(resultUrl))
                throw new Exception($"RequestState failed (empty result): {content}");

            string state = new ParameterBuilder(resultUrl).Get("state");
            if (string.IsNullOrEmpty(state))
                throw new Exception($"RequestState failed (no state in result URL): {resultUrl}");

            return (state, resultUrl);
        }

        // 预热登录会话：GET authorize.do 让服务端下发 USESSIONID / phlogact 等 Cookie。
        // 缺少这一步时，loginAndAuthorize 会被 4399 的 WAF 直接以 202「请稍后再试」拒绝。
        private async Task WarmUpLoginSessionAsync(string authorizeUrl)
        {
            if (string.IsNullOrEmpty(authorizeUrl))
                return;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, authorizeUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
                request.Headers.TryAddWithoutValidation("Referer", "https://m.4399api.com/");
                request.Headers.TryAddWithoutValidation(
                    "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");

                using var response = await _login.SendAsync(request);
                LogSys.Log($"[X4399Service] WarmUp authorize.do -> {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                // 预热失败不阻断主流程，仍让登录请求自己报错
                LogSys.Warn($"[X4399Service] WarmUp failed: {ex.Message}");
            }
        }

        public static string EncryptPassword(string message)
        {
            const string keyStr = "lzYW5qaXVqa";
            byte[] keyBytes = Encoding.UTF8.GetBytes(keyStr);

            // 1. 生成 8 字节随机盐
            byte[] salt = new byte[8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // 辅助方法：计算 MD5
            byte[] Md5(byte[] data)
            {
                using (var md5 = MD5.Create())
                {
                    return md5.ComputeHash(data);
                }
            }

            // 辅助方法：合并字节数组 (类似 Buffer.concat)
            byte[] Concat(params byte[][] arrays)
            {
                int totalLength = 0;
                foreach (var arr in arrays) totalLength += arr.Length;
                byte[] result = new byte[totalLength];
                int offset = 0;
                foreach (var arr in arrays)
                {
                    Buffer.BlockCopy(arr, 0, result, offset, arr.Length);
                    offset += arr.Length;
                }
                return result;
            }

            // 2. 派生 Key 和 IV (对应原 JS 的 hash1, hash2, hash3 逻辑)
            byte[] hash1 = Md5(Concat(keyBytes, salt));
            byte[] hash2 = Md5(Concat(hash1, keyBytes, salt));
            byte[] hash3 = Md5(Concat(hash2, keyBytes, salt));

            // calKey = Buffer.concat([hash1, hash2]).subarray(0, 32)
            byte[] calKey = new byte[32];
            byte[] combinedHash12 = Concat(hash1, hash2);
            Array.Copy(combinedHash12, 0, calKey, 0, Math.Min(combinedHash12.Length, 32));

            // iv = hash3.subarray(0, 16)
            byte[] iv = new byte[16];
            Array.Copy(hash3, 0, iv, 0, 16);

            // 3. AES-256-CBC 加密
            byte[] encrypted;
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = calKey;
                aes.IV = iv;

                using (var encryptor = aes.CreateEncryptor())
                {
                    byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                    encrypted = encryptor.TransformFinalBlock(messageBytes, 0, messageBytes.Length);
                }
            }

            // 4. 拼接最终报文: "Salted__" + salt + encrypted
            byte[] prefix = Encoding.UTF8.GetBytes("Salted__");
            byte[] finalResult = Concat(prefix, salt, encrypted);

            return Convert.ToBase64String(finalResult);
        }

        public async Task<string> LoginAndAuthorize(string username, string password, string? captcha = null, string? captchaId = null, int retry = 0)
        {
            if (retry > 5)
            {
                throw new Exception("Retry limit exceeded");
            }

            var (oauthState, authorizeUrl) = await RequestStateAndUrlAsync();

            // 关键：先 GET authorize.do 建立 ptlogin 会话（USESSIONID/phlogact）。
            // 缺少该会话时 loginAndAuthorize 会被 WAF 拦截并返回 202「请稍后再试」。
            await WarmUpLoginSessionAsync(authorizeUrl);

            // sec=1 时服务端按 AES 密文校验密码
            string securedPassword = EncryptPassword(password);

            ParameterBuilder parameterBuilder = new ParameterBuilder()
                .Append("isInputRealname", "false")
                .Append("isValidRealname", "false")
                .Append("sec", "1")
                .Append("password", securedPassword)
                .Append("username", username.ToLowerInvariant())
                .Append("css", "")
                .Append("show_close_button", "")
                .Append("response_type", "TOKEN")
                .Append("client_id", "40f9e9b95d6c71ba5c6e0bd14c0abeff")
                .Append("show_4399", "")
                .Append("username_history", "")
                .Append("uid", "")
                .Append("expand_ext_login_list", "")
                .Append("ref", "{\"game\":\"115716\",\"channel\":\"\"}")
                .Append("autoCreateAccount", "")
                .Append("scope", "basic")
                .Append("bizId", "2100001792")
                .Append("state", oauthState)
                .Append("show_ext_login", "")
                .Append("reg_mode", "reg_phone")
                .Append("show_back_button", "")
                .Append("auto_scroll", "")
                .Append("access_token", "")
                .Append("show_forget_password", "")
                .Append("auth_action", "ORILOGIN")
                .Append("redirect_uri",
                    "https://m.4399api.com/openapi/oauth-callback.html?gamekey=44770&game_key=115716")
                .Append("show_topbar", "false")
                .Append("aid", "")
                .Append("cid", "");
            if (captcha != null && captchaId != null)
            {
                parameterBuilder.Append("captcha", captcha).Append("captcha_id", captchaId);
            }
            string body = parameterBuilder.FormUrlEncode();

            LogSys.Log("[X4399Service] Executing loginAndAuthorize...");

            HttpContent content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");

            // 带上与浏览器一致的请求头，配合已建立的会话 Cookie，避免被 WAF 拦截
            using var loginRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "oauth2/loginAndAuthorize.do?channel=&sdk=op&sdk_version=3.12.2.503")
            {
                Content = content
            };
            loginRequest.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            loginRequest.Headers.TryAddWithoutValidation("Referer", authorizeUrl);
            loginRequest.Headers.TryAddWithoutValidation("Origin", "https://ptlogin.4399.com");
            loginRequest.Headers.TryAddWithoutValidation(
                "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            loginRequest.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");

            HttpResponseMessage redirect = await _login.SendAsync(loginRequest);

            string text = await redirect.Content.ReadAsStringAsync();

            LogSys.Log($"[X4399Service] HttpStatus:{redirect.StatusCode}");
            // 4399 返回的 body 可能含短信验证提示 / 实名信息，脱敏后再记录
            LogSys.Log($"[X4399Service] Content:{LogRedactor.Redact(text)}");

            // 4399 风控：202 Accepted + “请稍后再试”，此时既无重定向也无错误关键字，
            // 必须在判空之前单独识别，否则会被误报成 “no redirect location”。
            if (redirect.StatusCode == HttpStatusCode.Accepted || text.Contains("请稍后再试"))
            {
                throw new Exception(
                    "4399 服务端返回风控限制（请稍后再试）：请求被判定为异常，" +
                    "通常是登录过于频繁或当前网络环境被限制，请稍后重试或更换网络。");
            }

            if (text.Contains("身份证"))
            {
                throw new RealNameRequiredException("需要进行实名认证修复。", redirect.RequestMessage!.RequestUri.AbsolutePath);
            }

            if (text.Contains("密码错误"))
                throw new Exception("密码错误");

            if (text.Contains("用户不存在"))
                throw new Exception("用户不存在");

            if (text.Contains("验证码错误"))
                throw new Exception("验证码错误");

            if (text.Contains("验证码"))
            {
                throw new ShouldCaptchaException("需要填写验证码。", await GetCaptchaImageBytesWithHtml(username, password, text, retry));
                //return await HandleCaptchaWithHtml(username, password, text, retry);
            }

            if (redirect.Headers.Location == null)
                throw new Exception("Login failed: no redirect location");

            ParameterBuilder parameterBuilder2 = new ParameterBuilder(redirect.Headers.Location.AbsoluteUri);

            //LogSys.Log("aliydkagdwida::::::::::"+redirect.Headers.Location?.AbsoluteUri);

            if (captcha != null && captchaId == null)
            {
                parameterBuilder2.Append("captcha", captcha);

            }
            HttpResponseMessage obj = await _4399Api.GetAsync(parameterBuilder2.ToQueryUrl());

            obj.EnsureSuccessStatusCode();

            string text2 = await obj.Content.ReadAsStringAsync();

            if (text2.Contains("登录状态已失效，请重新登录"))
            {
                string state = await OAuthDevice();
                X4399Device value = new X4399Device
                {
                    DeviceIdentifier = _deviceIdentifier,
                    DeviceIdentifierSm = _deviceIdentifierSm,
                    DeviceUdid = _udid,
                    DeviceState = state
                };
                _state = state;

                await File.WriteAllTextAsync(
                    Path.Combine(Local4399Path, "4399com.cds"),
                    JsonSerializer.Serialize(value,
                    X4399DeviceContext.Default.X4399Device));

                return await LoginAndAuthorize(username, password);
            }
            if (text2.Contains("登录成功，但账号存在异常，需要验证"))
            {
                //throw new Exception($"[X4399Service] AccountShouldVerify: {text2}");
                return await LoginAndAuthorize(username, password, await CaptchaHandler.HandleLoginCaptchaAsync(text2), null, retry + 1);
            }

            // UserInfoResponse 含 idcard / realname / access_token / sessionid，全部脱敏
            LogSys.Log($"[X4399Service] UserInfoResponse:{LogRedactor.Redact(text2)}");

            X4399UserInfoResponse? entity4399UserInfoResponse = JsonSerializer.Deserialize(text2,
                X4399UserInfoResponseContext.Default.X4399UserInfoResponse);

            if (entity4399UserInfoResponse == null || !(entity4399UserInfoResponse.Code == "100"))
            {
                throw new Exception("[X4399Service] Failed to deserialize: " + text2);
            }

            if (entity4399UserInfoResponse.Result == null)
            {
                throw new Exception("[X4399Service] ?? What");
            }

            var user = entity4399UserInfoResponse.Result;

            //string randtime = "NULL";

            //var match = Regex.Match(text, @"parent\.timestamp\s*=\s*""(\d+)""");

            //if (match.Success)
            //{
            //    randtime = match.Groups[1].Value;
            //}

            //if (randtime == "NULL")
            //    throw new Exception("[X4399Service] Rand Time Regex Failed.");

            //string requestUri2 = $"https://ptlogin.4399.com/ptlogin/checkKidLoginUserCookie.do?appId=kid_wdsj&gameUrl=http://cdn.h5wan.4399sj.com/microterminal-h5-frame?game_id=500352&rand_time={randtime}&nick=null&onLineStart=false&show=1&isCrossDomain=1&retUrl=http%253A%252F%252Fptlogin.4399.com%252Fresource%252Fucenter.html%253Faction%253Dlogin%2526appId%253Dkid_wdsj%2526loginLevel%253D8%2526regLevel%253D8%2526bizId%253D2100001792%2526externalLogin%253Dqq%2526qrLogin%253Dtrue%2526layout%253Dvertical%2526level%253D101%2526css%253Dhttp%253A%252F%252Fmicrogame.5054399.net%252Fv2%252Fresource%252FcssSdk%252Fdefault%252Flogin.css%2526v%253D2018_11_26_16%2526postLoginHandler%253Dredirect%2526checkLoginUserCookie%253Dtrue%2526redirectUrl%253Dhttp%25253A%25252F%25252Fcdn.h5wan.4399sj.com%25252Fmicroterminal-h5-frame%25253Fgame_id%25253D500352%252526rand_time%25253D{randtime}";

            //_login.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0");
            //var request = await _login.GetAsync(requestUri2);
            //_login.DefaultRequestHeaders.Remove("User-Agent");

            //if (request.StatusCode != HttpStatusCode.Found)
            //{
            //    throw new Exception("[X4399Service] Check Login Status Failed.");
            //}

            //string? redirectUri = request.Headers.Location?.ToString();
            //if (string.IsNullOrEmpty(redirectUri))
            //{
            //    throw new Exception("[X4399Service] Get Redirect Uri Failed.");
            //}

            //LogSys.Log($"[X4399Service] Redirect Uri:{redirectUri}");

            //Uri redirectUriObj = new Uri(redirectUri);

            //if (redirectUriObj.Host != "cdn.h5wan.4399sj.com")
            //{
            //    throw new Exception($"[X4399Service] Redirect Uri Error, should <cdn.h5wan.4399sj.com> but <{redirectUriObj.Host}>");
            //}

            //var pairs = HttpUtility.ParseQueryString(redirectUriObj.Query);

            //string? sig = pairs["sig"];
            //string? uid = pairs["uid"];
            //string? time = pairs["time"];
            //string? validateState = pairs["validateState"];

            //if (string.IsNullOrEmpty(sig) ||
            //    string.IsNullOrEmpty(uid) ||
            //    string.IsNullOrEmpty(time) ||
            //    string.IsNullOrEmpty(validateState))
            //{
            //    throw new Exception($"[X4399Service] ParseQueryString got sth null.");
            //}

            // /X4399UserInfoResult? result = entity4399UserInfoResponse.Result;

            // /if (result == null)
            // /    throw new Exception("[X4399Service] UserInfoResult is null.");

            //string SDKrequestUri = $"https://microgame.5054399.net/v2/service/sdk/info?callback=&queryStr=game_id%3D500352%26nick%3Dnull%26sig%3D{sig}%26uid%3D{uid}%26fcm%3D0%26show%3D1%26isCrossDomain%3D1%26rand_time%3D{randtime}%26ptusertype%3D4399%26time%3D{time}%26validateState%3D{validateState}%26username%3D{username.ToLower()}&_={time}";

            //LogSys.Log($"[X4399Service] SDK Uri:{SDKrequestUri}");

            //var SDKresponse = await _login.GetAsync(SDKrequestUri);

            //var SDKContent = await SDKresponse.Content.ReadAsStringAsync();

            //LogSys.Log($"[X4399Service] SDKContent:{SDKContent}");

            //if (!SDKresponse.IsSuccessStatusCode)
            //    throw new Exception($"[X4399Service] StatusCode is not OK, StatusCode:{SDKresponse.StatusCode}");

            //if (string.IsNullOrEmpty(SDKContent))
            //    throw new Exception($"[X4399Service] Content is null or empty.");

            //X4399SDKResponse? SDKData = JsonSerializer.Deserialize(SDKContent,
            //                                                       X4399SDKResponseContext.Default.X4399SDKResponse);

            //if (SDKData == null)
            //    throw new Exception($"[X4399Service] SDK is null!");

            //if (SDKData.Code != 10000)
            //    throw new Exception($"[X4399Service] SDK Error ,Code:{SDKData.Code} Content:{SDKData.Message}");

            //if (SDKData.Data == null)
            //    throw new Exception($"[X4399Service] SDK->Data is null!");

            //if (string.IsNullOrEmpty(SDKData.Data.SdkLoginData))
            //    throw new Exception($"[X4399Service] SDK->Token is null!");

            //string? token = HttpUtility.ParseQueryString(SDKData.Data.SdkLoginData)["token"];

            //if (string.IsNullOrEmpty(token))
            //    throw new Exception($"[X4399Service] SDK->Token is null!");



            //F7hARtQ1CLzo----oCdQGiZeO9Tx
            var sdk_json = _mgbSdk.GenerateSAuth(username.ToLower(), user.Uid.ToString(), user.State , user.RegTime.ToString(), "allysdk.4399com","4399com", "ad");

            //var sdk_json_old = _mgbSdk.GenerateSAuthWithOld(username.ToLower(), uid, token, time, "4399pc", "pc");
            LogSys.Log($"[X4399Service] sdkJson:{LogRedactor.Redact(sdk_json)}");

            //LogSys.Log($"[X4399Service] sdkJsonold:{sdk_json_old}");

            //LogSys.Log($"[X4399Service] 这俩一样吗？ Result:{sdk_json == sdk_json_old}");

            // ---- 关键步骤：向网易换取正式会话 ----
            // pe-authentication 需要的是网易侧会话（oauth2.access_token + chl_sdk_json.sign），
            // 只拿渠道原始 sauth 去请求会被判定为 code 2001「客户端版本过低」。
            string? unisdkB64 = null;
            try
            {
                unisdkB64 = await _mgbSdk.AuthSession(sdk_json);
            }
            catch (InvalidOperationException)
            {
                // 渠道侧风控（403/subcode 15 等）：必须透传给 UI，
                // 不能退回原始 sauth，否则 pe-authentication 只会回「服务器维护中」，
                // 用户完全看不到真实原因。
                throw;
            }
            catch (Exception ex)
            {
                LogSys.Warn($"[X4399Service] AuthSession failed: {LogRedactor.Redact(ex.Message)}");
            }

            string sauthForPE = sdk_json;
            if (!string.IsNullOrEmpty(unisdkB64))
            {
                string? unisdkJson = MgbService.DecodeUnisdkLoginJson(unisdkB64);
                if (!string.IsNullOrEmpty(unisdkJson))
                {
                    LogSys.Log($"[X4399Service] unisdk_login_json:{LogRedactor.Redact(unisdkJson)}");
                    sauthForPE = MergeNeteaseSession(sdk_json, unisdkJson) ?? sdk_json;
                }
            }
            else
            {
                LogSys.Warn("[X4399Service] 未取得 unisdk_login_json，退回渠道原始 sauth");
            }

            SercurityAuth sauth = new SercurityAuth
            {
                sauth_json = sauthForPE,
            };

            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 关键点
                WriteIndented = false,
            };
            var context = new SercurityAuthContext(options);

            string finalJson = JsonSerializer.Serialize(sauth, context.SercurityAuth);

            //string newtonjson = JsonConvert.SerializeObject(sauth);

            // finalJson 里是完整的 sauth_json（含 sessionid / udid），只报摘要
            LogSys.Log($"用System.Text.json的：{LogRedactor.Brief(finalJson)}");
            //LogSys.Log($"用Newtonsoft.Json的：{newtonjson}");

            //LogSys.Log($"这俩一样吗？ Result:{finalJson == newtonjson}");

            return finalJson;
        }

        // 把网易侧会话（unisdk_login_json）合并进渠道 sauth。
        // 渠道 sauth 提供：app_channel / login_channel / sdkuid / sessionid（渠道凭据）
        // 网易会话提供：oauth2.access_token（JWT）/ chl_sdk_json.sign / aid / udid
        // 合并后 sauth 同时具备两层凭据，pe-authentication 才能通过。
        private static string? MergeNeteaseSession(string channelSauth, string unisdkJson)
        {
            try
            {
                using var chDoc = JsonDocument.Parse(channelSauth);
                using var unDoc = JsonDocument.Parse(unisdkJson);

                var map = new Dictionary<string, object?>();
                foreach (var p in chDoc.RootElement.EnumerateObject())
                    map[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();

                var un = unDoc.RootElement;
                var chl = un.TryGetProperty("chl_sdk_json", out var c) ? c : default;
                var oauth2 = un.TryGetProperty("oauth2", out var o) ? o : default;

                // 网易侧标识
                if (un.TryGetProperty("aid", out var aid))
                    map["aid"] = aid.ValueKind == JsonValueKind.String ? aid.GetString() : aid.ToString();
                if (un.TryGetProperty("sdkuid", out var sku))
                    map["sdkuid"] = sku.ValueKind == JsonValueKind.String ? sku.GetString() : sku.ToString();

                // 网易侧签名（chl_sdk_json.sign）
                if (chl.ValueKind == JsonValueKind.Object)
                {
                    if (chl.TryGetProperty("sign", out var sg) && sg.ValueKind == JsonValueKind.String)
                        map["sign"] = sg.GetString();
                    if (chl.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                        map["netease_timestamp"] = ts.GetString();
                    if (chl.TryGetProperty("udid", out var ud) && ud.ValueKind == JsonValueKind.String)
                        map["udid"] = ud.GetString();
                }

                // 网易侧 OAuth2 token
                if (oauth2.ValueKind == JsonValueKind.Object)
                {
                    if (oauth2.TryGetProperty("access_token", out var at) && at.ValueKind == JsonValueKind.String)
                        map["access_token"] = at.GetString();
                    if (oauth2.TryGetProperty("refresh_token", out var rt) && rt.ValueKind == JsonValueKind.String)
                        map["refresh_token"] = rt.GetString();
                }

                return JsonSerializer.Serialize(map);
            }
            catch (Exception ex)
            {
                LogSys.Warn($"[X4399Service] MergeNeteaseSession failed: {ex.Message}");
                return null;
            }
        }

        private async Task<byte[]> GetCaptchaImageBytesWithHtml(string username, string password, string html, int retry)
        {
            Match match = CaptchaRegex().Match(html);
            if (!match.Success)
            {
                throw new Exception("Cannot find captcha in html");
            }
            string matchedCaptchaId = match.Groups[1].Value;
            HttpResponseMessage obj = await _login.GetAsync("/ptlogin/captcha.do?captchaId=" + matchedCaptchaId + $"&xx={retry}");
            CurrentCaptchaId = matchedCaptchaId;
            //WebNexusApi nexus = _nexus;
            //string captcha = nexus.ComputeCaptchaAsync(await obj.Content.ReadAsByteArrayAsync());
            return await obj.Content.ReadAsByteArrayAsync();//await LoginAndAuthorize(username, password, captcha, matchedCaptchaId, retry + 1);
        }

        private static string GenerateIdentifier(DateTime? dateTime = null, string? additionalData = null)
        {
            string text = (dateTime ?? DateTime.Now).ToString("yyyyMMddHHmm");
            string text2 = GenerateHash50(additionalData);
            return text + text2;
        }

        private static string GenerateHash50(string? data = null)
        {
            if (string.IsNullOrEmpty(data))
            {
                data = Guid.NewGuid().ToString() + DateTime.Now.Ticks;
            }
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(data))).Substring(0, 50);
        }

        private static Regex CaptchaRegex()
        {
            return new Regex(
                @"captchaId=(captchaReq[0-9a-f]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            );
        }
    }
}
