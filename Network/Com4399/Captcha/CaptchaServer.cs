using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Elysium.Core.Utils;

namespace PLDUOBAO.Network.Com4399.Captcha
{
    internal class CaptchaServer
    {
        public static string JigsawCaptchaHtml = "<!DOCTYPE html>\r\n<html lang=\"zh\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>滑块验证码</title>\r\n    <style>\r\n        body {\r\n            font-family: Arial, sans-serif;\r\n            display: flex;\r\n            flex-direction: column;\r\n            align-items: center;\r\n            justify-content: center;\r\n            height: 100vh;\r\n            background-color: #f0f0f0;\r\n        }\r\n        #verification-container {\r\n            position: relative;\r\n            width: 544px;\r\n            height: 306px;\r\n            overflow: hidden;\r\n            border: 1px solid #ccc;\r\n            margin-bottom: 20px;\r\n        }\r\n        #verification-image {\r\n            width: 100%;\r\n            height: 100%;\r\n            background-size: cover;\r\n        }\r\n        #slider {\r\n            position: absolute;\r\n            width: 116px;\r\n            height: 100%; \r\n            background-size: cover;\r\n            cursor: pointer;\r\n            left: 0; \r\n            top: 0; \r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n\r\n    <div id=\"verification-container\">\r\n        <div id=\"verification-image\"></div>\r\n        <div id=\"slider\"></div>\r\n    </div>\r\n    <p>滑动距离: <span id=\"distance\">0</span> 像素</p>\r\n\r\n    <script>\r\n        const slider = document.getElementById('slider');\r\n        const distanceDisplay = document.getElementById('distance');\r\n        const verificationImageDiv = document.getElementById('verification-image');\r\n        let isDragging = false;\r\n        let startX = 0;\r\n        let totalDistance = 0;\r\n\r\n        fetch('/img.json')\r\n            .then(response => response.json())\r\n            .then(data => {\r\n                verificationImageDiv.style.backgroundImage = `url(${data.verificationImage})`;\r\n                slider.style.backgroundImage = `url(${data.sliderImage})`;\r\n            })\r\n            .catch(error => {\r\n                console.error('获取图片数据失败:', error);\r\n            });\r\n\r\n        slider.addEventListener('mousedown', (e) => {\r\n            isDragging = true;\r\n            startX = e.clientX - slider.offsetLeft;\r\n        });\r\n\r\n        document.addEventListener('mousemove', (e) => {\r\n            if (isDragging) {\r\n                let x = e.clientX - startX;\r\n                if (x < 0) x = 0;\r\n                if (x > 494) x = 494; // 544 - 50 (slider width)\r\n                slider.style.left = x + 'px';\r\n                totalDistance = x;\r\n                distanceDisplay.textContent = totalDistance;\r\n            }\r\n        });\r\n\r\n        document.addEventListener('mouseup', () => {\r\n            if (isDragging) {\r\n                isDragging = false;\r\n                console.log('用户滑动的总距离:', totalDistance);\r\n                submitDistance(totalDistance);\r\n            }\r\n        });\r\n\r\n        async function submitDistance(distance) {\r\n            const jsonData = JSON.stringify({ x: distance });\r\n            const encryptedData = await encryptData(jsonData);\r\n            fetch('/resolve', {\r\n                method: 'POST',\r\n                headers: {\r\n                    'Content-Type': 'application/json'\r\n                },\r\n                body: JSON.stringify({ data: encryptedData })\r\n            })\r\n            .then(response => response.json())\r\n            .then(data => {\r\n                console.log('服务器响应:', data);\r\n                window.close();\r\n            })\r\n            .catch(error => {\r\n                alert('提交失败:', error);\r\n            });\r\n        }\r\n\r\n        // 纯本地实现：AES-128-CBC + PKCS7，等价于原先外部脚本库的 CryptoJS 行为\r\n        const AES_KEY = new Uint8Array([0x52,0xC0,0xA4,0x9D,0x28,0x48,0x2A,0x6F,0x7B,0x16,0xF8,0x9A,0xCE,0xBB,0xAC,0xD5]);\r\n        const AES_IV = new TextEncoder().encode(\"79c83220d9974edf\");\r\n\r\n        async function encryptData(data) {\r\n            const key = await crypto.subtle.importKey(\"raw\", AES_KEY, { name: \"AES-CBC\" }, false, [\"encrypt\"]);\r\n            const cipher = await crypto.subtle.encrypt({ name: \"AES-CBC\", iv: AES_IV }, key, new TextEncoder().encode(data));\r\n            const bytes = new Uint8Array(cipher);\r\n            let binary = \"\";\r\n            for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);\r\n            return btoa(binary);\r\n        }\r\n    </script>\r\n</body>\r\n</html>";

        public static string ClickCaptchaHtml = "<!DOCTYPE html>\r\n<html lang=\"zh\">\r\n<head>\r\n    <meta charset=\"UTF-8\">\r\n    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    <title>点击文字验证</title>\r\n    <style>\r\n        #image-container {\r\n            position: relative;\r\n            width: 544px;\r\n            height: 306px;\r\n            overflow: hidden;\r\n        }\r\n        #target-image {\r\n            width: 100%;\r\n            height: 100%;\r\n        }\r\n        #clickable-text {\r\n            cursor: pointer;\r\n            margin-top: 10px;\r\n        }\r\n        .click-dot {\r\n            position: absolute;\r\n            width: 14px;\r\n            height: 14px;\r\n            background: rgba(255,0,0,0.7);\r\n            border-radius: 50%;\r\n            border: 2px solid #fff;\r\n            pointer-events: auto;\r\n            transform: translate(-50%, -50%);\r\n            z-index: 10;\r\n            display: flex;\r\n            align-items: center;\r\n            justify-content: center;\r\n            color: #fff;\r\n            font-size: 12px;\r\n            font-weight: bold;\r\n            user-select: none;\r\n        }\r\n    </style>\r\n</head>\r\n<body>\r\n\r\n<div id=\"image-container\">\r\n    <img id=\"target-image\" src=\"\" alt=\"验证图片\">\r\n</div>\r\n<div id=\"clickable-text\"></div>\r\n\r\n<script>\r\n    const targetImage = document.getElementById('target-image');\r\n    const clickableText = document.getElementById('clickable-text');\r\n    const imageContainer = document.getElementById('image-container');\r\n    let clicks = [];\r\n    let maxClicks = 4;\r\n    const width = 544;\r\n    const height = 306;\r\n\r\n    fetch('/img.json')\r\n        .then(response => response.json())\r\n        .then(data => {\r\n            targetImage.src = data.img;\r\n            clickableText.innerText = `请点击以下文字：${data.text}`;\r\n            maxClicks = data.text.split(',').length;\r\n        })\r\n        .catch(error => console.error('获取图片和文字失败:', error));\r\n\r\n    function renderDots() {\r\n        document.querySelectorAll('.click-dot').forEach(dot => dot.remove());\r\n        clicks.forEach((pt, idx) => {\r\n            const dot = document.createElement('div');\r\n            dot.className = 'click-dot';\r\n            dot.style.left = `${pt.x}px`;\r\n            dot.style.top = `${pt.y}px`;\r\n            dot.textContent = idx + 1;\r\n            dot.dataset.idx = idx;\r\n            dot.addEventListener('click', (e) => {\r\n                e.stopPropagation();\r\n                clicks = clicks.slice(0, idx);\r\n                renderDots();\r\n            });\r\n            imageContainer.appendChild(dot);\r\n        });\r\n    }\r\n\r\n    targetImage.addEventListener('click', (event) => {\r\n        if (clicks.length < maxClicks) {\r\n            const rect = targetImage.getBoundingClientRect();\r\n            const x = event.clientX - rect.left;\r\n            const y = event.clientY - rect.top;\r\n\r\n            clicks.push({ x: x, y: y });\r\n            renderDots();\r\n\r\n            if (clicks.length === maxClicks) {\r\n                submitData();\r\n            }\r\n        }\r\n    });\r\n\r\n    function submitData() {\r\n        const data = {\r\n            w: width,\r\n            h: height,\r\n            c: clicks\r\n        };\r\n\r\n        fetch('/resolve', {\r\n            method: 'POST',\r\n            headers: {\r\n                'Content-Type': 'application/json'\r\n            },\r\n            body: JSON.stringify({ data: JSON.stringify(data) })\r\n        })\r\n        .then(response => {\r\n            if (response.ok) {\r\n                console.log('数据提交成功');\r\n                window.close();\r\n            } else {\r\n                alert('数据提交失败');\r\n            }\r\n        })\r\n        .catch(error => {\r\n            alert('提交数据时发生错误:', error);\r\n        });\r\n    }\r\n</script>\r\n\r\n</body>\r\n</html>";

        private readonly HttpListener _httpListener;

        private bool _isRunning;

        public readonly int Port;

        public CaptchaServer(int port)
        {
            Port = port;
            string uriPrefix = $"http://127.0.0.1:{port}/";
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add(uriPrefix);
        }

        public Task StartAsync()
        {
            if (_isRunning)
            {
                return Task.CompletedTask;
            }
            _httpListener.Start();
            _isRunning = true;
            Task.Run(async delegate
            {
                while (_isRunning && _httpListener.IsListening)
                {
                    try
                    {
                        HttpListenerContext context = await _httpListener.GetContextAsync();
                        Task.Run(delegate
                        {
                            HandleHttpRequest(context);
                        });
                    }
                    catch (HttpListenerException)
                    {
                        break;
                    }
                    catch (Exception ex2)
                    {
                        LogSys.Err($"Error processing request: {ex2.Message}");
                    }
                }
            });
            return Task.CompletedTask;
        }

        public void Stop()
        {
            if (_isRunning)
            {
                _isRunning = false;
                _httpListener.Stop();
            }
        }

        private void HandleHttpRequest(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                if (request.Url == null)
                {
                    response.StatusCode = 400;
                    response.Close();
                    return;
                }
                string text = request.Url.AbsolutePath.Trim('/').ToLowerInvariant();
                if (text == null || text.Length != 0)
                {
                    switch (text)
                    {
                        case "index.html":
                            break;
                        case "img.json":
                            if (request.HttpMethod == "GET")
                            {
                                HandleImageJsonRequest(response);
                                return;
                            }
                            goto IL_00d0;
                        case "resolve":
                            if (request.HttpMethod == "POST")
                            {
                                HandleResolveRequest(request, response);
                                return;
                            }
                            goto IL_00d0;
                        default:
                            goto IL_00d0;
                    }
                }
                if (request.HttpMethod == "GET")
                {
                    HandleIndexRequest(response);
                    return;
                }
                goto IL_00d0;
            IL_00d0:
                response.StatusCode = 404;
                WriteStringToResponse(response, "Not Found");
            }
            catch (Exception ex)
            {
                try
                {
                    context.Response.StatusCode = 500;
                    WriteStringToResponse(context.Response, ex.ToString());
                }
                catch
                {
                    LogSys.Err("Server Error");
                }
            }
            finally
            {
                context.Response.Close();
            }
        }

        private void HandleIndexRequest(HttpListenerResponse response)
        {
            string content = ((CaptchaHandler.CurrentCaptchaType == "jigsaw") ? JigsawCaptchaHtml : ClickCaptchaHtml);
            response.ContentType = "text/html; charset=utf-8";
            WriteStringToResponse(response, content);
        }

        private void HandleImageJsonRequest(HttpListenerResponse response)
        {
            response.ContentType = "application/json";
            Dictionary<string, string> value = ((!(CaptchaHandler.CurrentCaptchaType == "jigsaw")) ? new Dictionary<string, string>
        {
            {
                "img",
                "data:image/png;base64," + CaptchaHandler.BackgroundImageBase64
            },
            {
                "text",
                CaptchaHandler.ClickableText
            }
        } : new Dictionary<string, string>
        {
            {
                "verificationImage",
                "data:image/png;base64," + CaptchaHandler.BackgroundImageBase64
            },
            {
                "sliderImage",
                "data:image/png;base64," + CaptchaHandler.SliderImageBase64
            }
        });
            WriteStringToResponse(response, JsonSerializer.Serialize(value,CaptchaResultContext.Default.DictionaryStringString));
        }

        private void HandleResolveRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.ContentType = "application/json";
            using StreamReader streamReader = new StreamReader(request.InputStream, request.ContentEncoding);
            using JsonDocument jsonDocument = JsonDocument.Parse(streamReader.ReadToEnd());
            CaptchaHandler.SetCaptchaResult(jsonDocument.RootElement.GetProperty("data").GetString() ?? "");
            WriteStringToResponse(response, "{\"result\":\"resolved\"}");
            Task.Delay(1000).ContinueWith(delegate
            {
                Stop();
            });
        }

        private static void WriteStringToResponse(HttpListenerResponse response, string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
        }
    }
}
