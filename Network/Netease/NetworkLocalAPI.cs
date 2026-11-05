using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.LocalAuthServer;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.SDK;
using PLDUOBAO.Util;
using Mark;
using Noya.LocalServer.Common.Extensions;
using Org.BouncyCastle.Asn1.Ocsp;

namespace PLDUOBAO.Network.Netease
{
    internal static class NetworkLocalAPI
    {
        public enum PostMode
        {
            Core,
            Client,
            Auth,
            UpdateAuth,
        }

        public static readonly HttpClient client = new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            Proxy = null,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public static AuthcationResponseEntity? CurrentAvaliableUser { get; set; } = null;

        public static string CurrentUserName { get; set; } = "";

        public static int CurrentLevel { get; set; } = -1;

        public static string? CurrentSAuth { get; set; } = "";

        public static UserCookie.CookiePlatform CurrentPlatform { get; set; } = UserCookie.CookiePlatform.None;

        public static string? Current4399Username { get; set; } = "";

        public static string? Current4399Password { get; set; } = "";

        public static DateTimeOffset CurrentUserLoginTime { get; set; } = DateTimeOffset.Now;
        public static void ResetUser()
        {
            CurrentAvaliableUser = null;
            CurrentUserName = "";
            MainWindow.UCMainmenu = null;
            CurrentSAuth = "";
            CurrentPlatform = UserCookie.CookiePlatform.None;
            CurrentLevel = -1;
        }

        public static async Task<T?> G79PostAsyncRegular<T>(PostMode mode, string requests, string url, JsonTypeInfo<T> jsonTypeInfo)
        {
            var response = await G79PostAsync(mode, requests, url);

            if (string.IsNullOrEmpty(response))
                throw new Exception("NetworkLocalAPI::G79PostAsync < Response is null or empty..> ");

            return JsonSerializer.Deserialize(response, jsonTypeInfo);
        }

        public static async Task<ApiResponse<T>?> G79PostAsync<T>(PostMode mode, string requests, string url, JsonTypeInfo<ApiResponse<T>> jsonTypeInfo)
        {
            var response = await G79PostAsync(mode, requests, url);

            if (string.IsNullOrEmpty(response))
                throw new Exception("NetworkLocalAPI::G79PostAsync < Response is null or empty..> ");

            return JsonSerializer.Deserialize(response, jsonTypeInfo);
        }

        public static async Task<string?> G79PostAsync(PostMode mode, string requests, string url)
        {
            LogSys.Log($"Request URL:{url}");
            LogSys.Log($"Request Body:{requests}");

            string baseUrl = "";
            switch (mode)
            {
                case PostMode.Core:
                    baseUrl = PEAuth.GetLoginSercurity().PE_url!;
                    break;
                case PostMode.Client:
                    baseUrl = "https://g79mclobt.minecraft.cn";
                    break;
                case PostMode.Auth:
                    baseUrl = "https://g79authobt.minecraft.cn";
                    break;
                default:
                    throw new NotImplementedException("WHO DID THIS FUCK THING???");
            }

            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.token))
            {
                LogSys.Err("Token为空。");
                return null;
            }

            var headers = TokenUtil.ComputeHttpRequestToken(
                url,
                requests,
                CurrentAvaliableUser.entity_id,
                CurrentAvaliableUser.token,
                mode == PostMode.Auth);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url)
            {
                Content = new StringContent(requests)
            };

            if (mode == PostMode.Auth)
            {
                request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url)
                {
                    Content = new StringContent(x19Crypt.HttpEncrypt_g79v12(Encoding.UTF8.GetBytes(requests)).ToHex())
                };
                request.Headers.ExpectContinue = false;
                LogSys.Log("Enable Auth mode.");
            }

            LogSys.Log($"Full Request URL:{request.RequestUri?.ToString()}");

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // 添加 User-Agent 到请求头
            request.Headers.UserAgent.ParseAdd("libhttpclient/1.0.0.0");

            // 添加自定义 token 相关头
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HttpResponseMessage result = new HttpResponseMessage();
            result = await client.SendAsync(request);


            var response = await result.Content.ReadAsStringAsync();

            LogSys.Log($"TryRequest:{url} Response:{response}");

            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"遭遇了非200状态码。 页面状态：{result.StatusCode}");
            }

            if (mode == PostMode.Auth)
            {
                byte[] responseBytes = response.HexToBytes();
                return x19Crypt.HttpDecrypt_g79v12(responseBytes);
            }

            return response;
        }

        public async static Task<string?> HttpPost_v2(string url, string param, string host)
        {
            

            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.token))
            {
                LogSys.Err("Token为空。");
                return null;
            }

            HttpClient httpClient = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                Proxy = null,
            });
            HttpContent httpContent = new StringContent(x19Crypt.HttpEncrypt_g79v12(Encoding.UTF8.GetBytes(param)).ToHex());
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            httpContent.Headers.Add("user-token", Encoding.ASCII.GetBytes(x19Crypt.ComputeDynamicToken(url, param, CurrentAvaliableUser.token)).ToHex(toUpper: true));
            httpContent.Headers.Add("user-id", CurrentAvaliableUser.entity_id);
            httpClient.DefaultRequestHeaders.Add("User-Agent", "libhttpclient/1.0.0.0");
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            string fullUrl = host + url;

            HttpResponseMessage response = new();

            //if (!ChatServer.isServiceRunning)
            //{
            //    await ChatServer.StartLinkServiceAsync();
            //}
            //ChatServer.Do_Login();

            //await Task.Delay(2000);

            response = await httpClient.PostAsync(fullUrl, httpContent);
            
            // 2. 发送 POST 请求（同步方式；可替换成 async 方式）
             

            // 3. 检查状态码（非 2xx 则抛出异常或处理）
            if (!response.IsSuccessStatusCode)
            {
                // 你可以换成其他异常类型，或记录日志等
                throw new HttpRequestException($"请求失败，状态码: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            // 4. 读取响应内容字符串
            string responseHex = await response.Content.ReadAsStringAsync();

            // 5. 可选：赋值给 array（取决于上下文）
            return responseHex;

            //// 6. 转换为字节数组
            //byte[] responseBytes = responseHex.HexToBytes();

            //// 7. 解密字节数组并返回结果
            //return x19Crypt.HttpDecrypt_g79v12(responseBytes);
        }

        public static async Task<T?> X19PostAsync<T>(PostMode mode, string requests, string url, JsonTypeInfo<T> jsonTypeInfo)
        {
            var response = await X19PostAsync(mode, requests, url);

            if (string.IsNullOrEmpty(response))
                throw new Exception("NetworkLocalAPI::G79PostAsync < Response is null or empty..> ");

            return JsonSerializer.Deserialize(response, jsonTypeInfo);
        }

        public static async Task<string?> X19PostAsync(PostMode mode, string requests, string url)
        {
            LogSys.Log($"Request URL:{url}");
            LogSys.Log($"Request Body:{requests}");

            string baseUrl = "";
            switch (mode)
            {
                case PostMode.UpdateAuth:
                case PostMode.Core:
                    baseUrl = "https://x19obtcore.nie.netease.com:8443";
                    break;
                case PostMode.Client:
                    baseUrl = "https://x19mclobt.nie.netease.com";
                    break;
                default:
                    throw new NotImplementedException("WHO DID THIS FUCK THING???");
            }

            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.token))
            {
                LogSys.Err("Token为空。");
                return null;
            }

            var headers = TokenUtil.ComputeHttpRequestToken(
                url,
                requests,
                CurrentAvaliableUser.entity_id,
                CurrentAvaliableUser.token,
                mode == PostMode.Auth);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url)
            {
                Content = new StringContent(requests)
            };

            if (mode == PostMode.UpdateAuth)
            {
                request = new HttpRequestMessage(HttpMethod.Post, baseUrl + url)
                {
                    Content = new ByteArrayContent(x19Crypt.HttpEncrypt(Encoding.UTF8.GetBytes(requests)))
                };
            }

            LogSys.Log($"Full Request URL:{request.RequestUri?.ToString()}");

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // 添加 User-Agent 到请求头
            request.Headers.UserAgent.ParseAdd("WPFLauncher/0.0.0.0");

            // 添加自定义 token 相关头
            foreach (var header in headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            HttpResponseMessage result = new HttpResponseMessage();
            result = await client.SendAsync(request);


            var response = await result.Content.ReadAsStringAsync();

            var responseBytes = await result.Content.ReadAsByteArrayAsync();

            if (mode == PostMode.UpdateAuth)
            {
                var decryptedBytes = x19Crypt.HttpDecrypt(responseBytes);

                if (decryptedBytes != null)
                    response = Encoding.UTF8.GetString(decryptedBytes);
            }

            LogSys.Log($"TryRequest:{url} Response:{response}");

            if (!result.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"遭遇了非200状态码。 页面状态：{result.StatusCode}");
            }

            return response;
        }

        public const string GatewayBaseUrl = "https://x19apigatewayobt.nie.netease.com";

        public static async Task<T?> GatewayPostAsync<T>(string url, string requests, JsonTypeInfo<T> jsonTypeInfo)
        {
            var response = await GatewayRequestAsync(HttpMethod.Post, url, requests);

            if (string.IsNullOrEmpty(response))
                throw new Exception("NetworkLocalAPI::GatewayPostAsync < Response is null or empty..> ");

            return JsonSerializer.Deserialize(response, jsonTypeInfo);
        }

        public static async Task<string?> GatewayRequestAsync(HttpMethod method, string url, string requests = "")
        {
            LogSys.Log($"Gateway Request URL:{url}");
            LogSys.Log($"Gateway Request Body:{requests}");

            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.token))
            {
                LogSys.Err("Token为空。");
                return null;
            }

            var headers = TokenUtil.ComputeHttpRequestToken(
                url,
                requests,
                CurrentAvaliableUser.entity_id,
                CurrentAvaliableUser.token);

            var request = new HttpRequestMessage(method, GatewayBaseUrl + url);
            if (method == HttpMethod.Post)
                request.Content = new StringContent(requests);

            LogSys.Log($"Full Gateway Request URL:{request.RequestUri?.ToString()}");

            if (request.Content != null)
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            request.Headers.UserAgent.ParseAdd("WPFLauncher/0.0.0.0");
            request.Headers.TryAddWithoutValidation("x_trace_id", StringGenerator.GenerateRandomString(32));

            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            var result = await client.SendAsync(request);
            var response = await result.Content.ReadAsStringAsync();

            LogSys.Log($"Gateway TryRequest:{url} Response:{response}");

            if (!result.IsSuccessStatusCode)
                throw new HttpRequestException($"遭遇了非200状态码。 页面状态：{result.StatusCode}");

            return response;
        }
    }
}