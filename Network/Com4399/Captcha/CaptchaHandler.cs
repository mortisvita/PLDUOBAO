using Elysium.Core.Utils;
using PLDUOBAO.Network.Com4399.SDK;
using PLDUOBAO.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.Captcha
{
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class CaptchaResultContext : JsonSerializerContext
    {

    }

    internal class CaptchaHandler
    {

        private static readonly HttpClient _4399Api = new HttpClient()
        {
            BaseAddress = new Uri("https://m.4399api.com/")
        };

        private static TaskCompletionSource<string>? _captchaTaskCompletionSource;

        private static CancellationTokenSource? _cancellationTokenSource;

        public static string BackgroundImageBase64 { get; private set; } = "";

        public static string SliderImageBase64 { get; private set; } = "";

        public static string ClickableText { get; private set; } = "";

        public static string CurrentCaptchaType { get; private set; } = "jigsaw";

        public static void SetCaptchaResult(string data)
        {
            _captchaTaskCompletionSource?.SetResult(data);
        }

        private static async Task<string> WaitForCaptchaCompletionAsync(int timeoutSeconds = 300)
        {
            _captchaTaskCompletionSource = new TaskCompletionSource<string>();
            _cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            _cancellationTokenSource.Token.Register(delegate
            {
                _captchaTaskCompletionSource?.TrySetCanceled();
            });
            try
            {
                return await _captchaTaskCompletionSource.Task;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"Captcha verification timeout after {timeoutSeconds} seconds");
            }
        }

        private static void ResetCaptchaState()
        {
            BackgroundImageBase64 = "";
            SliderImageBase64 = "";
            ClickableText = "";
        }

        public static async Task<string> HandleLoginCaptchaAsync(string resultJson)
        {
            if (string.IsNullOrEmpty(resultJson))
            {
                LogSys.Log("Result JSON is null or empty");
                return resultJson;
            }
            using JsonDocument document = JsonDocument.Parse(resultJson);
            JsonElement rootElement = document.RootElement;
            if (!rootElement.TryGetProperty("code", out var value) || value.GetString() != "103")
            {
                return resultJson;
            }
            string text = null;
            if (rootElement.TryGetProperty("result", out var value2) && value2.TryGetProperty("url", out var value3))
            {
                text = value3.GetString();
            }
            if (string.IsNullOrEmpty(text))
            {
                LogSys.Log("Captcha URL is null or empty");
                return resultJson;
            }
            if (text.Contains("jigsaw"))
            {
                CurrentCaptchaType = "jigsaw";
                resultJson = await HandleJigsawCaptchaAsync(text);
            }
            else if (text.Contains("click"))
            {
                CurrentCaptchaType = "click";
                resultJson = await HandleClickCaptchaAsync(text);
            }
            else
            {
                LogSys.Log("Unknown captcha type");
            }
            return resultJson;
        }

        private static async Task<string> HandleJigsawCaptchaAsync(string captchaUrl)
        {
            
            string text = await (await _4399Api.GetAsync(captchaUrl)).Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(text))
            {
                LogSys.Log("Captcha response is null or empty");
                return "";
            }
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement rootElement = document.RootElement;
            BackgroundImageBase64 = "";
            SliderImageBase64 = "";
            string captchaId = "";
            if (rootElement.TryGetProperty("result", out var value))
            {
                if (value.TryGetProperty("img", out var value2))
                {
                    BackgroundImageBase64 = value2.GetString() ?? "";
                }
                if (value.TryGetProperty("img2", out var value3))
                {
                    SliderImageBase64 = value3.GetString() ?? "";
                }
                if (value.TryGetProperty("captchaId", out var value4))
                {
                    captchaId = value4.GetString() ?? "";
                }
            }
            CurrentCaptchaType = "jigsaw";
            if (string.IsNullOrEmpty(BackgroundImageBase64) || string.IsNullOrEmpty(SliderImageBase64))
            {
                LogSys.Log("Captcha images are null or empty");
                return "";
            }
            CaptchaServer server = await StartCaptchaServerAsync();
            try
            {
                string value5 = await WaitForCaptchaCompletionAsync();
                string domain = "https://m.4399api.com/captcha/jigsaw-check.html?refer=sdk&v=" + UrlEncoder.Default.Encode(value5) + "&captchaId=" + UrlEncoder.Default.Encode(captchaId);
                string text2 = await (await _4399Api.GetAsync(domain)).Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(text2))
                {
                    LogSys.Log("Check response is null or empty");
                    return "";
                }
                using JsonDocument checkDocument = JsonDocument.Parse(text2);
                JsonElement rootElement2 = checkDocument.RootElement;
                if (!rootElement2.TryGetProperty("code", out var value6) || value6.GetInt32() != 100)
                {
                    LogSys.Log("Captcha check failed, retrying");
                    return await HandleJigsawCaptchaAsync(captchaUrl);
                }
                string token = "";
                if (rootElement2.TryGetProperty("result", out var value7) && value7.TryGetProperty("token", out var value8))
                {
                    token = value8.GetString() ?? "";
                }
                return BuildCaptchaParameter(token, captchaId);
            }
            finally
            {
                server.Stop();
                ResetCaptchaState();
            }
        }

        private static async Task<string> HandleClickCaptchaAsync(string captchaUrl)
        {
            string text = await (await _4399Api.GetAsync(captchaUrl)).Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(text))
            {
                LogSys.Log("Captcha response is null or empty");
                return "";
            }
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement rootElement = document.RootElement;
            BackgroundImageBase64 = "";
            ClickableText = "";
            string captchaId = "";
            if (rootElement.TryGetProperty("result", out var value))
            {
                if (value.TryGetProperty("img", out var value2))
                {
                    BackgroundImageBase64 = value2.GetString() ?? "";
                }
                if (value.TryGetProperty("text", out var value3))
                {
                    ClickableText = value3.GetString() ?? "";
                }
                if (value.TryGetProperty("captchaId", out var value4))
                {
                    captchaId = value4.GetString() ?? "";
                }
            }
            CurrentCaptchaType = "click";
            if (string.IsNullOrEmpty(BackgroundImageBase64) || string.IsNullOrEmpty(ClickableText))
            {
                LogSys.Log("Captcha image or click text is null or empty");
                return "";
            }
            CaptchaServer server = await StartCaptchaServerAsync();
            try
            {
                string value5 = await WaitForCaptchaCompletionAsync();
                string domain = "https://m.4399api.com/captcha/click-check.html?refer=sdk&v=" + UrlEncoder.Default.Encode(value5) + "&captchaId=" + UrlEncoder.Default.Encode(captchaId);
                //using HttpWrapper checkWrapper = new HttpWrapper(domain);
                string text2 = await (await _4399Api.GetAsync(domain)).Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(text2))
                {
                    LogSys.Log("Check response is null or empty");
                    return "";
                }
                using JsonDocument checkDocument = JsonDocument.Parse(text2);
                JsonElement rootElement2 = checkDocument.RootElement;
                if (!rootElement2.TryGetProperty("code", out var value6) || value6.GetInt32() != 100)
                {
                    LogSys.Log("Captcha check failed, retrying");
                    return await HandleClickCaptchaAsync(captchaUrl);
                }
                string token = "";
                if (rootElement2.TryGetProperty("result", out var value7) && value7.TryGetProperty("token", out var value8))
                {
                    token = value8.GetString() ?? "";
                }
                return BuildCaptchaParameter(token, captchaId);
            }
            finally
            {
                server.Stop();
                ResetCaptchaState();
            }
        }

        private static string BuildCaptchaParameter(string token, string captchaId)
        {
            return JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "v_token", token },
                { "captcha_id", captchaId },
                { "type", "0" }
            },CaptchaResultContext.Default.DictionaryStringString);
        }

        private static async Task<CaptchaServer> StartCaptchaServerAsync()
        {
            int port = NetworkUtil.GetAvailablePort();
            while (true)
            {
                try
                {
                    CaptchaServer server = new CaptchaServer(port);
                    await server.StartAsync();
                    LogSys.Log($"Captcha HTTP server started on port {port}");
                    string fileName = $"http://127.0.0.1:{port}/";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = fileName,
                        UseShellExecute = true
                    });
                    return server;
                }
                catch (HttpListenerException)
                {
                    port = NetworkUtil.GetAvailablePort();
                }
            }
        }
    }
}
