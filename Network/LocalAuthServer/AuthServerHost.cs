using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.Util;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;
using PLDUOBAO.Network.Netease.NetworkSDK;
using Mark;
using Noya.LocalServer.Common.Extensions;

namespace PLDUOBAO.Network.LocalAuthServer
{
    public static class AuthServerHost
    {
        private const string PythonPayloadFileName = "api-ms-win-core-runtime-l1-1-0.dll";

        private static readonly string PythonCompatibilityCode = $"""
            # coding=utf-8

            def __instant__code__():
                import os
                __platform = 2

                import sys
                import common.gameConfig as modGameCfg
                import _record
                import _setting
                import setting

                # EC's safe_runtime rejects Python functions placed on built-in
                # modules because functions expose __code__. Callable instances
                # behave like functions without exposing __code__ themselves.
                class __constant_callable__(object):
                    def __init__(self, value):
                        self.value = value

                    def __call__(self, *args, **kwargs):
                        return self.value

                class __input_mode_callable__(object):
                    def __init__(self, original):
                        self.original = original

                    def __call__(self, option_id):
                        if option_id == "INPUT_MODE":
                            return 1
                        return self.original(option_id)

                class __path_exists_callable__(object):
                    def __init__(self, original, windows_dir):
                        self.original = original
                        self.windows_dir = os.path.normcase(os.path.abspath(windows_dir))

                    def __call__(self, path):
                        try:
                            normalized = os.path.normcase(os.path.abspath(path))
                            if normalized == self.windows_dir:
                                return False
                        except Exception:
                            pass
                        return self.original(path)

                if getattr(sys, "_plduobao_android_compat_patched", False):
                    return

                sys.platform = "android"
                modGameCfg.RUNTIME_PLATFORM = __platform
                modGameCfg.B_NO_LAUNCHER = False

                # Patch the native-facing modules directly: EC reads _record,
                # not minecraft/record.py. Do not use lambda here; safe_runtime
                # scans both _record and _setting for members with __code__.
                _setting.is_nolauncher = __constant_callable__(False)
                _record.get_os_name = __constant_callable__("android")
                _record.get_patch_version = __constant_callable__("{VerificationProfile.PatchVersion}")
                _record.get_engine_version = __constant_callable__("{VerificationProfile.EngineVersion}")

                # NukkitClient obtains INPUT_MODE through the public setting
                # wrapper, so patching this one entry covers PlayerView as well.
                setting.get_toggle_option = __input_mode_callable__(setting.get_toggle_option)

                # awaUtils separately checks whether the real Windows directory
                # exists. Hide only that path and preserve every other exists().
                __windows_dir = os.getenv("WINDIR") or "C:\\Windows"
                os.path.exists = __path_exists_callable__(os.path.exists, __windows_dir)

                sys._plduobao_android_compat_patched = True

            __instant__code__()
            """;

        private static HttpListener? httpListener;
        private static CancellationTokenSource? cts;
        public static int CurrentRunningPort = 0;
        public static bool IsRental {  get; private set; }

        public static string ServerItemId { get; private set; } = "";
        public static Process? proc { get; set; } = null;

        public static void StartAsync(int port,string serverItemId,bool isRental = false)
        {
            try
            {
                if (httpListener?.IsListening == true)
                {
                    Stop();
                }

                CurrentRunningPort = port;
                ServerItemId = serverItemId;
                IsRental = isRental;

                string prefix = $"http://127.0.0.1:{port}/";
                httpListener = new HttpListener();
                httpListener.Prefixes.Add(prefix);
                httpListener.Start();

                cts = new CancellationTokenSource();

                LogSys.Log($"[AuthServer] 服务端启动成功，监听地址：{prefix} 时间：{DateTime.Now}");

                _ = Task.Run(() => HandleRequestsAsync(httpListener, cts.Token));          
            }
            catch (Exception ex)
            {
                LogSys.Err($"[AuthServer] 启动失败: {ex}");
            }
        }

        public static async Task HandleRequestsAsync(HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    // 你自己的处理逻辑（等效于 Result()）
                    await HandleRequest(context);
                }
                catch (HttpListenerException ex) when (ex.ErrorCode == 995)
                {
                    // 被取消
                    break;
                }
                catch (Exception ex)
                {
                    LogSys.Err($"[AuthServer] 处理请求异常: {ex}");
                }
            }

            LogSys.Log("[AuthServer] 已停止处理请求");
        }

        public static void Stop()
        {
            try
            {
                cts?.Cancel();
                httpListener?.Stop();
                httpListener?.Close();
                CurrentRunningPort = 0;
                IsRental = false;
                ServerItemId = "";
                LogSys.Log("[AuthServer] 服务已停止");
            }
            catch (Exception ex)
            {
                LogSys.Err($"[AuthServer] 停止时异常: {ex}");
            }
        }

        private static async Task HandleRequest(HttpListenerContext context)
        {
            // 替代你原来的 Result()
            //context.Response.StatusCode = 200;
            //using var writer = new StreamWriter(context.Response.OutputStream);
            //writer.Write("OK");
            //context.Response.Close();
            var request = context.Request;
            var response = context.Response;

            string rid = Guid.NewGuid().ToString();

            LogSys.Log($"[AuthServer] 新的请求：Rid={rid} Time={DateTime.Now.ToString()} URL={request.RawUrl}");

            response.ContentType = "text/plain;charset=UTF-8";
            response.AddHeader("Content-type", "text/plain");
            response.ContentEncoding = Encoding.UTF8;

            string responseText = "";

            if (request.HttpMethod != "POST" || !request.HasEntityBody)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                responseText = "Only POST requests with body are supported.";
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                try
                {
                    responseText = await HandleGameRequest(request,
                                                     response,
                                                     true);

                    if (File.Exists(PathUtils.SpoofTool64Path) &&
                        File.Exists(PathUtils.PyCodeExecuter64Path))
                    {
                        // 读取配置来决定是否启用设备绕过
                        var config = PathUtils.LoadConfig();
                        bool enableSpoof = config?.enableDeviceSpoof ?? false;  // 默认禁用
                        
                        if (enableSpoof)
                        {
                            string? gamePath = await Dispatcher.UIThread.InvokeAsync(
                                () => MainWindow.UCMainmenu?.GamePath);
                            if (string.IsNullOrWhiteSpace(gamePath))
                            {
                                throw new InvalidOperationException("Game path is unavailable.");
                            }

                            await File.WriteAllTextAsync(
                                Path.Combine(gamePath, PythonPayloadFileName),
                                PythonCompatibilityCode,
                                Encoding.UTF8);

                            await Task.Run(async () =>
                            {
                                if (proc == null)
                                    return;

                                var process = new Process();
                                process.StartInfo.FileName = PathUtils.SpoofTool64Path;
                                process.StartInfo.Arguments = $"-pid={proc.Id} -os=1 -model=\"{RandomDeviceSpoof.GetRandomDevice()}\" ";
                                process.StartInfo.UseShellExecute = false;
                                process.StartInfo.CreateNoWindow = false;

                                process.Start();
                                await process.WaitForExitAsync();
                            });
                        }
                    }
                }
                catch (Exception ex) 
                {
                    LogSys.Err(ex);
                }
            }

            byte[] buffer = Encoding.UTF8.GetBytes(responseText);
            response.ContentType = "text/plain; charset=UTF-8";
            response.ContentEncoding = Encoding.UTF8;
            response.ContentLength64 = buffer.Length;

            using Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Flush();
        }

        private static async Task<string> HandleGameRequest(HttpListenerRequest request, 
                                                HttpListenerResponse response, 
                                                bool Encrypt)
        {
            using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
            string rawRequestBody = reader.ReadToEnd();

            if (string.IsNullOrWhiteSpace(rawRequestBody))
            {
                //SetErrorResponse(response, 400, "Empty request body");
                response.StatusCode = 400;
                return "请求体为空";
            }

            string jsonPayload = Encrypt
                ? x19Crypt.HttpDecrypt_g79v12(rawRequestBody.HexToBytes())
                : rawRequestBody;

            LogSys.Log($"[AuthServer] 接收到请求数据：{jsonPayload}");

            AuthcationG79? authOrg = null;

            try
            {
                authOrg = JsonSerializer.Deserialize(jsonPayload, AuthcationG79Context.Default.AuthcationG79);
            }
            catch (Exception ex) 
            {
                LogSys.Err(ex);
            }

            if (authOrg == null)
                return "FAILED!";

            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return "FAILED!";
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return "FAILED!";
            }

            AuthcationG79 authPayload = new AuthcationG79()
            {
                bit = "64",
                clientKey = authOrg.clientKey,
                displayName = authOrg.displayName,
                engineVersion = PEAuth.GetLoginSercurity().engineVersion,
                netease_sid = $"{ServerItemId}:{(IsRental ? "RentalGame" : "NetworkGame")}",
                os_name = "android",
                patchVersion = PEAuth.GetLoginSercurity().patchVersion,
                uid = long.Parse(CurrentAvaliableUser.entity_id),
            };

            var responseToken = await NeteasePosts.AuthcationG79V2Async(authPayload);

            if (string.IsNullOrEmpty(responseToken))
            {
                LogSys.Err("[AuthServer] 认证失败，响应结果为空。");
                return "FAILED!";
            }

            response.StatusCode = 200;
            response.StatusDescription = "OK";

            LogSys.Log($"[AuthServer] 认证完成：{DateTime.Now}");

            return responseToken;
        }

    }
}
