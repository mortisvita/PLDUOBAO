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

                    // 设备/平台标识改写：纯 C# 进程内存数据补丁。
                    // 只读写目标进程的数据字节，不注入、不建远程线程、不改代码。
                    var config = PathUtils.LoadConfig();
                    if (config?.enableDeviceSpoof ?? false)
                    {
                        try
                        {
                            DeviceIdentity.Ensure();
                            DeviceProfile.SelectPersistent(
                                config.deviceModelKey, config.deviceBrand,
                                DeviceIdentity.Udid, DeviceIdentity.Mac);

                            if (proc != null && !proc.HasExited)
                            {
                                PlatformPatcher.Start(proc.Id);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogSys.Err($"[PlatformPatcher] {ex.Message}");
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
