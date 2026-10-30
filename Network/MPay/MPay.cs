using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Login.Login.NetEase;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Login.Login.MPay;

public static class MPay
{

    private static readonly Random _random = new Random();
    internal class EntityUsersParameters
    {
        public string password { get; set; }
        public string unique_id { get; set; }
        public string username { get; set; }
    }
    public static int NewMPayDeviceInfo()
    {
        try
        {
            HttpHelper.InitHttp();
            string parameters = GenerateParameters();
            string url = "https://service.mkey.163.com/mpay/games/aecfrxodyqaaaajp-g-x19/devices";
            Function.ClientLog("[MPay] Request:" + parameters);
            string response = HttpHelper.postAPImclobtPE(url, parameters);
            Function.ClientLog("[MPay] Response:" + response);

            if (string.IsNullOrEmpty(response))
            {
                Function.ClientLog("[MPay] MPay设备信息请求失败：响应为空");
                return 0;
            }

            JObject jsonObj = JObject.Parse(response);
            JToken deviceToken = jsonObj["device"];

            if (deviceToken == null)
            {
                Function.ClientLog("[MPay] MPay设备信息请求失败：响应格式不正确");
                return 0;
            }

            string deviceId = deviceToken["id"]?.ToString();
            string deviceKey = deviceToken["key"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceKey))
            {
                Function.ClientLog("[MPay] MPay设备信息请求失败：id或key为空");
                return 0;
            }

            // 生成unique字段
            string unique = Guid.NewGuid().ToString().Replace("-", "");

            Function.ClientLog($"[MPay]：id={deviceId}, key={deviceKey}, unique={unique}");

            // 构建JSON字符串，包含id、key和unique三个字段
            string jsonString = $"{{\"id\" : \"{deviceId}\",\"key\" : \"{deviceKey}\",\"unique\" : \"{unique}\"}}";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonString);
            string base64String = Convert.ToBase64String(jsonBytes);

            // 设置到app.Temp
            app.Temp.MpayDeviceInfo = base64String;

            // 更新config.json
            try
            {
                JObject existingConfig = null;
                if (File.Exists("config.json"))
                {
                    string existingJson = File.ReadAllText("config.json");
                    existingConfig = JObject.Parse(existingJson);
                }
                else
                {
                    existingConfig = new JObject();
                }

                existingConfig["MpayDeviceInfo"] = base64String;
                string configJson = existingConfig.ToString(Formatting.None);
                File.WriteAllText("config.json", configJson);

                Function.ClientLog($"已更新config.json");
            }
            catch (Exception configEx)
            {
                Function.ClientError($"更新config.json失败：{configEx.Message}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            Function.ClientError($"[MPay] Error:{ex.Message}");
            return 0;
        }
    }
    public static string GetSauth_QR()
    {
        try
        {
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();
            string deviceKey = deviceInfo["key"]?.ToString();
            string uniqueId = deviceInfo["unique"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceKey) || string.IsNullOrEmpty(uniqueId))
            {
                Console.WriteLine("错误：MPay设备信息不完整");
                return null;
            }

            Console.WriteLine("正在获取二维码");

            // 第一步：获取二维码（已集成尺寸/颜色校验及控制台文本）
            string qrCodeResult = GetQRCode();
            JObject qrCodeJson = JObject.Parse(qrCodeResult);
            int qrSuccess = qrCodeJson["success"]?.ToObject<int>() ?? 0;

            if (qrSuccess != 1)
            {
                string reason = qrCodeJson["reason"]?.ToString() ?? "未知错误";
                Console.WriteLine($"获取二维码失败：{reason}");
                return null;
            }

            string uuid = qrCodeJson["uuid"]?.ToString();
            string qrcode = qrCodeJson["qrcode"]?.ToString();

            if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(qrcode))
            {
                Console.WriteLine("错误：获取的二维码信息不完整");
                return null;
            }

            // 直接输出二维码文本到控制台（无降级链接）
            Console.WriteLine("请扫描以下二维码：");
            Console.WriteLine(qrcode);
            Console.WriteLine("\n等待扫描二维码...");

            // 第二步：轮询二维码状态（保持不变）
            int lastStatus = -1;
            int queryStatus = 2;
            bool loginSuccess = false;
            string sauthResult = null;

            while (!loginSuccess)
            {
                string statusResult = CheckQRCodeStatus(uuid, queryStatus);
                JObject statusJson = JObject.Parse(statusResult);
                int statusSuccess = statusJson["success"]?.ToObject<int>() ?? 0;

                if (statusSuccess == 0)
                {
                    string reason = statusJson["reason"]?.ToString() ?? "未知错误";
                    Console.WriteLine($"查询二维码状态失败：{reason}");
                    return null;
                }
                else if (statusSuccess == 2)
                {
                    int currentStatus = statusJson["status"]?.ToObject<int>() ?? 0;

                    if (lastStatus == 0 && currentStatus == 1)
                    {
                        Console.WriteLine("请在手机上确认登录");
                    }

                    lastStatus = currentStatus;
                    queryStatus = currentStatus;
                    Thread.Sleep(1000);
                }
                else if (statusSuccess == 1)
                {
                    string code = statusJson["code"]?.ToString();
                    string loginChannel = statusJson["login_channel"]?.ToString();

                    if (string.IsNullOrEmpty(code))
                    {
                        Console.WriteLine("错误：登录成功但code为空");
                        return null;
                    }

                    Function.ClientLog($"获取到code: {code}, login_channel: {loginChannel}");

                    if (loginChannel == "netease")
                    {
                        string tokenResult = GetMPayToken_QR_netease(code);
                        JObject tokenJson = JObject.Parse(tokenResult);
                        int tokenSuccess = tokenJson["success"]?.ToObject<int>() ?? 0;

                        if (tokenSuccess == 1)
                        {
                            string token = tokenJson["token"]?.ToString();
                            string userId = tokenJson["id"]?.ToString();

                            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
                            {
                                Console.WriteLine("错误：获取的token或id为空");
                                return null;
                            }

                            string udid = GenerateHexString(32, true);
                            string aimInfoJson = "{\\\\\"aim\\\\\":\\\\\"100.100.100.100\\\\\",\\\\\"country\\\\\":\\\\\"CN\\\\\",\\\\\"tz\\\\\":\\\\\"+0800\\\\\",\\\\\"tzid\\\\\":\\\\\"\\\\\"}";

                            StringBuilder sauthBuilder = new StringBuilder();
                            sauthBuilder.Append("{");
                            sauthBuilder.Append($"\"gameid\":\"x19\",");
                            sauthBuilder.Append($"\"login_channel\":\"netease\",");
                            sauthBuilder.Append($"\"app_channel\":\"netease\",");
                            sauthBuilder.Append($"\"platform\":\"pc\",");
                            sauthBuilder.Append($"\"sdkuid\":\"{userId}\",");
                            sauthBuilder.Append($"\"sessionid\":\"{token}\",");
                            sauthBuilder.Append($"\"sdk_version\":\"3.9.0\",");
                            sauthBuilder.Append($"\"udid\":\"{udid}\",");
                            sauthBuilder.Append($"\"deviceid\":\"{deviceId}\",");
                            sauthBuilder.Append($"\"aim_info\":\"{aimInfoJson}\"");
                            sauthBuilder.Append("}");

                            string sauthJsonStr = sauthBuilder.ToString();
                            sauthResult = $"{{\"sauth_json\":\"{sauthJsonStr.Replace("\"", "\\\"")}\"}}";
                            loginSuccess = true;
                            Console.WriteLine("二维码登录成功！");
                        }
                        else
                        {
                            string reason = tokenJson["reason"]?.ToString() ?? "未知错误";
                            Console.WriteLine($"获取Token失败：{reason}");
                            return null;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"渠道服登录: {loginChannel}");

                        string channelResult = GetMPaySauth_QR_channel(code);
                        JObject channelJson = JObject.Parse(channelResult);
                        int channelSuccess = channelJson["success"]?.ToObject<int>() ?? 0;

                        if (channelSuccess == 1)
                        {
                            string sauth = channelJson["sauth"]?.ToString();
                            byte[] sauthBytes = Convert.FromBase64String(sauth);
                            sauth = Encoding.UTF8.GetString(sauthBytes);
                            if (string.IsNullOrEmpty(sauth))
                            {
                                Console.WriteLine("错误：获取的sauth为空");
                                return null;
                            }

                            var sauthObj = new { sauth_json = sauth };
                            sauthResult = JsonConvert.SerializeObject(sauthObj);
                            loginSuccess = true;
                            Console.WriteLine("二维码登录成功！");
                            Console.WriteLine("注意：渠道服cookie具有时效性，cookie失效后需要重新扫码获取");
                        }
                        else
                        {
                            string reason = channelJson["reason"]?.ToString() ?? "未知错误";
                            Console.WriteLine($"登录失败：{reason}");
                            return null;
                        }
                    }
                }
            }

            Console.WriteLine("登录成功");
            return sauthResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取sauth时发生错误：{ex.Message}");
            return null;
        }
    }
    public static string GetSauth()
    {
        try
        {
            // 检查MPay设备信息是否存在
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();
            string deviceKey = deviceInfo["key"]?.ToString();
            string uniqueId = deviceInfo["unique"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceKey) || string.IsNullOrEmpty(uniqueId))
            {
                Console.WriteLine("错误：MPay设备信息不完整");
                return null;
            }

            // 选择登录方式
            Console.WriteLine("请选择登录方式：");
            Console.WriteLine("[1] 邮箱登录");
            Console.WriteLine("[2] 短信验证登录");
            Console.Write("请输入序号：");

            string choice = Console.ReadLine();
            string token = null;
            string userId = null;

            if (choice == "1")
            {
                // 邮箱登录
                Console.Write("请输入邮箱地址：");
                string username = Console.ReadLine();

                Console.Write("请输入密码：");
                string password = Console.ReadLine();

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    Console.WriteLine("错误：邮箱或密码不能为空");
                    return null;
                }

                string result = GetMPayToken_Email(username, password);
                JObject resultJson = JObject.Parse(result);
                int success = resultJson["success"]?.ToObject<int>() ?? 0;

                if (success == 1)
                {
                    token = resultJson["token"]?.ToString();
                    userId = resultJson["id"]?.ToString();

                    if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
                    {
                        Console.WriteLine("错误：获取的token或id为空");
                        return null;
                    }

                    Console.WriteLine("邮箱登录成功！");
                }
                else
                {
                    string reason = resultJson["reason"]?.ToString() ?? "未知错误";
                    Console.WriteLine($"邮箱登录失败：{reason}");
                    return null;
                }
            }
            else if (choice == "2")
            {
                // 短信登录
                string nationcode = "86";

                Console.Write("请输入手机号：");
                string phonenumber = Console.ReadLine();

                if (string.IsNullOrEmpty(phonenumber))
                {
                    Console.WriteLine("错误：手机号不能为空");
                    return null;
                }

                string urs_udid = GenerateHexString(32, true);
                string mcount_transaction_id = Guid.NewGuid().ToString();

                Random random = new Random();
                int processId = random.Next(100, 65536);

                Console.WriteLine("正在发送短信验证码...");
                string sendResult = SendSMS(nationcode, phonenumber, urs_udid, mcount_transaction_id, processId.ToString());

                JObject sendJson = JObject.Parse(sendResult);
                int sendSuccess = sendJson["success"]?.ToObject<int>() ?? 0;

                if (sendSuccess != 1)
                {
                    string reason = sendJson["reason"]?.ToString() ?? "未知错误";
                    Console.WriteLine($"发送短信失败：{reason}");
                    return null;
                }

                Console.WriteLine("短信验证码已发送！");

                bool verified = false;
                string ticket = null;

                while (!verified)
                {
                    Console.Write("请输入6位验证码（输入exit退出）：");
                    string input = Console.ReadLine();

                    if (input.ToLower() == "exit")
                    {
                        Console.WriteLine("已退出验证码输入");
                        return null;
                    }

                    if (input.Length != 6 || !int.TryParse(input, out int verifycode))
                    {
                        Console.WriteLine("错误：验证码必须是6位数字");
                        continue;
                    }

                    // 验证短信验证码
                    Console.WriteLine("正在验证短信验证码...");
                    string verifyResult = VerifySMS(verifycode, nationcode, phonenumber, urs_udid, mcount_transaction_id, processId.ToString());

                    JObject verifyJson = JObject.Parse(verifyResult);
                    int verifySuccess = verifyJson["success"]?.ToObject<int>() ?? 0;

                    if (verifySuccess == 1)
                    {
                        ticket = verifyJson["ticket"]?.ToString();
                        if (!string.IsNullOrEmpty(ticket))
                        {
                            verified = true;
                            Console.WriteLine("短信验证码验证成功！");
                        }
                        else
                        {
                            Console.WriteLine("错误：获取的ticket为空");
                            return null;
                        }
                    }
                    else
                    {
                        string reason = verifyJson["reason"]?.ToString() ?? "未知错误";
                        Console.WriteLine($"验证失败：{reason}");
                        Console.WriteLine("请重新输入验证码或输入exit退出");
                    }
                }

                if (!string.IsNullOrEmpty(ticket))
                {
                    Console.WriteLine("正在获取Token...");
                    string tokenResult = GetMPayToken_SMS(ticket, nationcode, phonenumber, urs_udid, mcount_transaction_id, processId.ToString());

                    JObject tokenJson = JObject.Parse(tokenResult);
                    int tokenSuccess = tokenJson["success"]?.ToObject<int>() ?? 0;

                    if (tokenSuccess == 1)
                    {
                        token = tokenJson["token"]?.ToString();
                        userId = tokenJson["id"]?.ToString();

                        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
                        {
                            Console.WriteLine("错误：获取的token或id为空");
                            return null;
                        }

                        Console.WriteLine("短信登录成功！");
                    }
                    else
                    {
                        string reason = tokenJson["reason"]?.ToString() ?? "未知错误";
                        Console.WriteLine($"获取Token失败：{reason}");
                        return null;
                    }
                }
            }
            else
            {
                Console.WriteLine("无效的选择");
                return null;
            }

            string udid = GenerateHexString(32, true);

            string aimInfoJson = "{\\\\\"aim\\\\\":\\\\\"100.100.100.100\\\\\",\\\\\"country\\\\\":\\\\\"CN\\\\\",\\\\\"tz\\\\\":\\\\\"+0800\\\\\",\\\\\"tzid\\\\\":\\\\\"\\\\\"}";

            // 手动构建sauth_json字符串，不使用JObject来避免格式化
            StringBuilder sauthBuilder = new StringBuilder();
            sauthBuilder.Append("{");
            sauthBuilder.Append($"\"gameid\":\"x19\",");
            sauthBuilder.Append($"\"login_channel\":\"netease\",");
            sauthBuilder.Append($"\"app_channel\":\"netease\",");
            sauthBuilder.Append($"\"platform\":\"pc\",");
            sauthBuilder.Append($"\"sdkuid\":\"{userId}\",");
            sauthBuilder.Append($"\"sessionid\":\"{token}\",");
            sauthBuilder.Append($"\"sdk_version\":\"3.9.0\",");
            sauthBuilder.Append($"\"udid\":\"{udid}\",");
            sauthBuilder.Append($"\"deviceid\":\"{deviceId}\",");
            sauthBuilder.Append($"\"aim_info\":\"{aimInfoJson}\"");
            sauthBuilder.Append("}");

            string sauthJsonStr = sauthBuilder.ToString();

            // 创建最终的JSON字符串
            string resultJsonStr = $"{{\"sauth_json\":\"{sauthJsonStr.Replace("\"", "\\\"")}\"}}";

            Console.WriteLine("登录成功");
            return resultJsonStr;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"获取sauth时发生错误：{ex.Message}");
            return null;
        }
    }
    public static string GetMPayToken_Email(string username, string password)
    {
        try
        {
            HttpHelper.InitHttp();
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();
            string deviceKey = deviceInfo["key"]?.ToString();
            string uniqueId = deviceInfo["unique"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceKey) || string.IsNullOrEmpty(uniqueId))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息不完整\"}";
            }
            string url = $"https://service.mkey.163.com/mpay/games/aecfrxodyqaaaajp-g-x19/devices/{deviceId}/users";
            string mcountTransactionId = Guid.NewGuid().ToString();
            Random random = new Random();
            int processId = random.Next(100, 65536);
            byte[] usernameBytes = Encoding.UTF8.GetBytes(username);
            string un = Convert.ToBase64String(usernameBytes);
            EntityUsersParameters paramsObj = new EntityUsersParameters
            {
                username = username,
                password = password.EncodeMd5(),
                unique_id = uniqueId
            };
            string paramsJson = Newtonsoft.Json.JsonConvert.SerializeObject(paramsObj);

            byte[] aesKey = DecodeHex(deviceKey);

            string encryptedParams = EncodeAes(paramsJson, aesKey).EncodeHex();

            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=netease&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x32&");
            parameters.Append($"cv=c3.9.0&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.0.0.0&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={mcountTransactionId}&");
            parameters.Append($"opt_fields=nickname%2Cavatar%2Crealname_status%2Cmobile_bind_status&");
            parameters.Append($"params={Uri.EscapeDataString(encryptedParams)}&");
            parameters.Append($"process_id={processId}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"un={Uri.EscapeDataString(un)}&");
            parameters.Append($"updater_cv=c1.0.0");

            // 发送请求
            Function.ClientLog($"[MPay] 登录请求URL: {url}");
            Function.ClientLog($"[MPay] 请求参数: {parameters}");

            string response = HttpHelper.postAPImclobtPE(url, parameters.ToString());
            Function.ClientLog($"[MPay] 登录响应: {response}");

            if (string.IsNullOrEmpty(response))
            {
                return "{\"success\":0,\"reason\":\"服务器无响应\"}";
            }

            // 解析响应
            JObject responseObj = JObject.Parse(response);

            // 检查是否有token字段和id字段
            JToken userToken = responseObj["user"]?["token"];
            JToken userId = responseObj["user"]?["id"];

            if (userToken != null && userId != null)
            {
                string token = userToken.ToString();
                string id = userId.ToString();
                return $"{{\"success\":1,\"token\":\"{token}\",\"id\":\"{id}\"}}";
            }

            string errorReason = responseObj["reason"]?.ToString() ?? "未知错误";

            return $"{{\"success\":0,\"reason\":\"{errorReason}\"}}";
        }
        catch (Exception ex)
        {
            Function.ClientError($"[MPay] GetMPayToken错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }

    public static string GetMPayToken_SMS(string ticket, string nationcode, string phonenumber, string urs_udid, string mcount_transaction_id, string process_id)
    {
        try
        {
            HttpHelper.InitHttp();
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();

            if (string.IsNullOrEmpty(deviceId))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息中缺少device_id\"}";
            }

            // 构造un参数（nationcode-phonenumber的base64）
            string mobile = $"{nationcode}-{phonenumber}";
            byte[] mobileBytes = Encoding.UTF8.GetBytes(mobile);
            string un = Convert.ToBase64String(mobileBytes);

            // 构造请求URL
            string url = $"https://service.mkey.163.com/mpay/api/users/login/mobile/finish?un={Uri.EscapeDataString(un)}";

            // 构造参数字符串
            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=a50_sdk_cn&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x32&");
            parameters.Append($"cv=a5.9.0&");
            parameters.Append($"device_id={Uri.EscapeDataString(deviceId)}&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.0.0.0&");
            parameters.Append($"login_for=1&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={Uri.EscapeDataString(mcount_transaction_id)}&");
            parameters.Append($"opt_fields=nickname%2Cavatar%2Crealname_status%2Cmobile_bind_status&");
            parameters.Append($"process_id={Uri.EscapeDataString(process_id)}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"ticket={Uri.EscapeDataString(ticket)}&");
            parameters.Append($"updater_cv=c1.0.0&");
            parameters.Append($"urs_udid={Uri.EscapeDataString(urs_udid)}");

            // 初始化HTTP

            // 发送请求
            Function.ClientLog($"[GetMPayToken_SMS] 请求URL: {url}");
            Function.ClientLog($"[GetMPayToken_SMS] 请求参数: {parameters}");

            string response = HttpHelper.postAPImclobtPE(url, parameters.ToString());
            Function.ClientLog($"[GetMPayToken_SMS] 响应: {response}");

            if (string.IsNullOrEmpty(response))
            {
                return "{\"success\":0,\"reason\":\"服务器无响应\"}";
            }
            JObject responseObj;
            try
            {
                responseObj = JObject.Parse(response);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查是否有token字段和id字段
            JToken userToken = responseObj["user"]?["token"];
            JToken userId = responseObj["user"]?["id"];

            if (userToken != null && userId != null)
            {
                string token = userToken.ToString();
                string id = userId.ToString();
                return $"{{\"success\":1,\"token\":\"{token}\",\"id\":\"{id}\"}}";
            }

            if (responseObj["code"] != null)
            {
                string errorReason = responseObj["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorReason}\"}}";
            }

            return $"{{\"success\":0,\"reason\":\"响应中未找到token和id字段\"}}";
        }
        catch (Exception ex)
        {
            Function.ClientError($"[GetMPayToken_SMS] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string GetMPayToken_QR_netease(string code)
    {
        try
        {
            // 初始化HTTP
            HttpHelper.InitHttp();

            // 检查MPay设备信息是否存在
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();

            if (string.IsNullOrEmpty(deviceId))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息中缺少device_id\"}";
            }

            // 第一步：用code换取ticket
            string ticketUrl = $"https://loadingbaycn.webapp.163.com/app/v1/third_party_account/ticket?code={Uri.EscapeDataString(code)}&app_id=81&device_id={Uri.EscapeDataString(deviceId)}";

            Function.ClientLog($"[GetMPayToken_QR_netease] ticket URL: {ticketUrl}");

            string ticketResponse = HttpHelper.getAPImclobtPE(ticketUrl);

            if (string.IsNullOrEmpty(ticketResponse))
            {
                return "{\"success\":0,\"reason\":\"获取ticket请求无响应\"}";
            }


            // 解析ticket响应
            JObject ticketJson;
            try
            {
                ticketJson = JObject.Parse(ticketResponse);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"获取ticket响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查返回的code字段
            int responseCode = ticketJson["code"]?.ToObject<int>() ?? 0;
            if (responseCode != 200)
            {
                string errorMsg = ticketJson["msg"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorMsg}\"}}";
            }

            // 获取ticket
            string ticket = ticketJson["data"]?["ticket"]?.ToString();
            if (string.IsNullOrEmpty(ticket))
            {
                return "{\"success\":0,\"reason\":\"ticket响应中缺少ticket字段\"}";
            }

            Function.ClientLog($"[GetMPayToken_QR_netease] 获取到ticket: {ticket}");

            // 第二步：用ticket换取token
            // 生成必要的随机参数
            Random random = new Random();
            string mcountTransactionId = Guid.NewGuid().ToString();
            int processId = random.Next(100, 65536);
            string transid_uuid = GenerateHexString(32, true);
            string transid = transid_uuid + "_1767196800000_100000000";
            string uniTransactionId = transid_uuid + "_1767196800000_100000000";

            // 构造POST参数
            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=a50_sdk_cn&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x64&");
            parameters.Append($"cv=c4.16.0&");
            parameters.Append($"device_id={Uri.EscapeDataString(deviceId)}&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.21.2.0&");
            parameters.Append($"is_remember_account=0&");
            parameters.Append($"jf_game_id=x19&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={Uri.EscapeDataString(mcountTransactionId)}&");
            parameters.Append($"opt_fields=nickname%2Cavatar%2Crealname_status%2Cmobile_bind_status%2Cmask_related_mobile%2Crelated_login_status&");
            parameters.Append($"process_id={processId}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"ticket={Uri.EscapeDataString(ticket)}&");
            parameters.Append($"transid={Uri.EscapeDataString(transid)}&");
            parameters.Append($"uni_transaction_id={Uri.EscapeDataString(uniTransactionId)}&");
            parameters.Append($"updater_cv=c1.0.0");

            string loginUrl = "https://service.mkey.163.com/mpay/api/users/login/ticket";

            Function.ClientLog($"[GetMPayToken_QR_netease] 登录URL: {loginUrl}");
            Function.ClientLog($"[GetMPayToken_QR_netease] 参数: {parameters}");

            string loginResponse = HttpHelper.postAPImclobtPE(loginUrl, parameters.ToString());

            if (string.IsNullOrEmpty(loginResponse))
            {
                return "{\"success\":0,\"reason\":\"登录请求无响应\"}";
            }


            // 解析登录响应
            JObject loginJson;
            try
            {
                loginJson = JObject.Parse(loginResponse);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"登录响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查是否有错误信息
            if (loginJson["code"] != null)
            {
                string errorReason = loginJson["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorReason}\"}}";
            }

            // 检查是否有user.token字段和user.id字段
            JToken userToken = loginJson["user"]?["token"];
            JToken userId = loginJson["user"]?["id"];

            if (userToken != null && userId != null)
            {
                string token = userToken.ToString();
                string id = userId.ToString();
                return $"{{\"success\":1,\"token\":\"{token}\",\"id\":\"{id}\"}}";
            }

            return "{\"success\":0,\"reason\":\"响应中未找到token和id字段\"}";
        }
        catch (Exception ex)
        {
            Function.ClientError($"[GetMPayToken_QR_netease] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string GetMPaySauth_QR_channel(string code)
    {
        try
        {
            // 初始化HTTP
            HttpHelper.InitHttp();

            // 检查MPay设备信息是否存在
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();
            string deviceKey = deviceInfo["key"]?.ToString();

            if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(deviceKey))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息不完整\"}";
            }

            // 第一步：用code换取ticket（与GetMPayToken_QR_netease相同）
            string ticketUrl = $"https://loadingbaycn.webapp.163.com/app/v1/third_party_account/ticket?code={Uri.EscapeDataString(code)}&app_id=81&device_id={Uri.EscapeDataString(deviceId)}";

            Function.ClientLog($"[GetMPaySauth_QR_channel] ticket URL: {ticketUrl}");

            string ticketResponse = HttpHelper.getAPImclobtPE(ticketUrl);

            if (string.IsNullOrEmpty(ticketResponse))
            {
                return "{\"success\":0,\"reason\":\"获取ticket请求无响应\"}";
            }


            // 解析ticket响应
            JObject ticketJson;
            try
            {
                ticketJson = JObject.Parse(ticketResponse);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"获取ticket响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查返回的code字段
            int responseCode = ticketJson["code"]?.ToObject<int>() ?? 0;
            if (responseCode != 200)
            {
                string errorMsg = ticketJson["msg"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorMsg}\"}}";
            }

            // 获取ticket
            string ticket = ticketJson["data"]?["ticket"]?.ToString();
            if (string.IsNullOrEmpty(ticket))
            {
                return "{\"success\":0,\"reason\":\"ticket响应中缺少ticket字段\"}";
            }

            Function.ClientLog($"[GetMPaySauth_QR_channel] ticket: {ticket}");

            // 第二步：用ticket换取sauth
            // 生成必要的随机参数
            Random random = new Random();
            string mcountTransactionId = "1"; // 示例中是1
            int processId = random.Next(10000, 65536);
            string transid_uuid = GenerateHexString(32, true);
            string transid = transid_uuid + "_1767196800000_100000000";
            string uniTransactionId = transid_uuid + "_1767196800000_100000000";

            // 对ticket进行AES加密
            byte[] aesKey = DecodeHex(deviceKey);
            string encryptCode = EncodeAes(ticket, aesKey).EncodeHex();

            // 构造POST参数
            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=a50_sdk_cn&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x64&");
            parameters.Append($"cv=c4.16.0&");
            parameters.Append($"device_id={Uri.EscapeDataString(deviceId)}&");
            parameters.Append($"encrypt_code={Uri.EscapeDataString(encryptCode)}&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.21.2.0&");
            parameters.Append($"jf_game_id=x19&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={Uri.EscapeDataString(mcountTransactionId)}&");
            parameters.Append($"opt_fields=nickname%2Cavatar%2Crealname_status%2Cmobile_bind_status%2Cmask_related_mobile%2Crelated_login_status&");
            parameters.Append($"process_id={processId}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"transid={Uri.EscapeDataString(transid)}&");
            parameters.Append($"uni_transaction_id={Uri.EscapeDataString(uniTransactionId)}&");
            parameters.Append($"updater_cv=c1.0.0");

            string exchangeUrl = "https://service.mkey.163.com/mpay/api/users/login/qrcode/exchange_token";

            Function.ClientLog($"[GetMPaySauth_QR_channel] 交换token URL: {exchangeUrl}");
            Function.ClientLog($"[GetMPaySauth_QR_channel] 参数: {parameters}");

            string exchangeResponse = HttpHelper.postAPImclobtPE(exchangeUrl, parameters.ToString());

            if (string.IsNullOrEmpty(exchangeResponse))
            {
                return "{\"success\":0,\"reason\":\"交换token请求无响应\"}";
            }


            // 解析交换token响应
            JObject exchangeJson;
            try
            {
                exchangeJson = JObject.Parse(exchangeResponse);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"交换token响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查是否有错误信息
            if (exchangeJson["code"] != null)
            {
                string errorReason = exchangeJson["reason"]?.ToString() ?? "未知错误";
                string errorCode = exchangeJson["code"]?.ToString() ?? "未知错误码";
                return $"{{\"success\":0,\"reason\":\"{errorCode}: {errorReason}\"}}";
            }

            // 提取SAUTH_JSON，优先从user中提取，其次从ext_info中提取
            string sauthJsonBase64 = null;

            // 先尝试从user中提取
            JToken userToken = exchangeJson["user"];
            if (userToken != null)
            {
                // user.client_data是base64编码的，里面可能包含SAUTH_JSON
                string clientDataBase64 = userToken["client_data"]?.ToString();
                if (!string.IsNullOrEmpty(clientDataBase64))
                {
                    try
                    {
                        byte[] clientDataBytes = Convert.FromBase64String(clientDataBase64);
                        string clientData = Encoding.UTF8.GetString(clientDataBytes);
                        JObject clientDataJson = JObject.Parse(clientData);

                        // client_data中可能包含SAUTH_JSON
                        // 但根据示例，SAUTH_JSON在ext_info的extra_unisdk_data中
                    }
                    catch
                    {
                        // 忽略解析错误
                    }
                }

                // 检查user.pc_ext_info.extra_unisdk_data
                JToken pcExtInfo = userToken["pc_ext_info"];
                if (pcExtInfo != null)
                {
                    string extraUnisdkData = pcExtInfo["extra_unisdk_data"]?.ToString();
                    if (!string.IsNullOrEmpty(extraUnisdkData))
                    {
                        // 解析extra_unisdk_data（它是一个JSON字符串）
                        try
                        {
                            JObject extraUnisdkJson = JObject.Parse(extraUnisdkData);
                            sauthJsonBase64 = extraUnisdkJson["SAUTH_JSON"]?.ToString();
                        }
                        catch
                        {
                            // 忽略解析错误
                        }
                    }
                }
            }

            // 如果user中没有找到，从ext_info中提取
            if (string.IsNullOrEmpty(sauthJsonBase64))
            {
                JToken extInfoToken = exchangeJson["ext_info"];
                if (extInfoToken != null)
                {
                    string extraUnisdkData = extInfoToken["extra_unisdk_data"]?.ToString();
                    if (!string.IsNullOrEmpty(extraUnisdkData))
                    {
                        try
                        {
                            JObject extraUnisdkJson = JObject.Parse(extraUnisdkData);
                            sauthJsonBase64 = extraUnisdkJson["SAUTH_JSON"]?.ToString();
                        }
                        catch
                        {
                            // 忽略解析错误
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(sauthJsonBase64))
            {
                return "{\"success\":0,\"reason\":\"响应中未找到SAUTH_JSON\"}";
            }

            // Base64解码SAUTH_JSON
            try
            {
                // URL解码（因为SAUTH_JSON可能包含%3D等URL编码字符）
                sauthJsonBase64 = Uri.UnescapeDataString(sauthJsonBase64);

                // 解码Base64
                byte[] sauthBytes = Convert.FromBase64String(sauthJsonBase64);
                string sauthJson = Encoding.UTF8.GetString(sauthBytes);

                // 验证解码后的内容是否为有效JSON
                JObject.Parse(sauthJson);

                return $"{{\"success\":1,\"sauth\":\"{sauthJsonBase64}\"}}";
            }
            catch (Exception decodeEx)
            {
                return $"{{\"success\":0,\"reason\":\"SAUTH_JSON解码失败: {decodeEx.Message}\"}}";
            }
        }
        catch (Exception ex)
        {
            Function.ClientError($"[GetMPaySauth_QR_channel] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string GetQRCode()
    {
        try
        {
            HttpHelper.InitHttp();

            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
                NewMPayDeviceInfo();

            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);
            string deviceId = deviceInfo["id"]?.ToString();

            if (string.IsNullOrEmpty(deviceId))
                return "{\"success\":0,\"reason\":\"MPay设备信息中缺少device_id\"}";

            // 1. 创建二维码登录，获取 uuid
            string createUrl = $"https://service.mkey.163.com/mpay/api/qrcode/create_login?app_channel=a50_sdk_cn&qrcode_channel_type=2&dst_jf_game_id=x19&cv=c0.0.0&is_remember=2&device_id={Uri.EscapeDataString(deviceId)}&game_id=aecglf6ee4aaaarz-g-a50";
            Function.ClientLog($"[GetQRCode] 创建二维码URL: {createUrl}");
            string createResponse = HttpHelper.getAPImclobtPE(createUrl);
            if (string.IsNullOrEmpty(createResponse))
                return "{\"success\":0,\"reason\":\"创建二维码请求无响应\"}";

            JObject createJson = JObject.Parse(createResponse);
            if (createJson["code"] != null)
            {
                string errorReason = createJson["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"创建二维码失败: {errorReason}\"}}";
            }

            string uuid = createJson["uuid"]?.ToString();
            if (string.IsNullOrEmpty(uuid))
                return "{\"success\":0,\"reason\":\"创建二维码响应中缺少uuid字段\"}";

            // 2. 获取二维码图片
            string imageUrl = $"https://service.mkey.163.com/mpay/api/qrcode/image?uuid={Uri.EscapeDataString(uuid)}&game_id=aecglf6ee4aaaarz-g-a50&cv=c0.0.0";

            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("image/png"));
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = httpClient.SendAsync(request).Result;
                var contentType = response.Content.Headers.ContentType?.MediaType;

                if (contentType != "image/png")
                {
                    string errorResponse = response.Content.ReadAsStringAsync().Result;
                    Function.ClientLog($"[GetQRCode] 获取二维码图片失败，Content-Type: {contentType}, 响应: {errorResponse}");
                    try
                    {
                        JObject errorJson = JObject.Parse(errorResponse);
                        string reason = errorJson["reason"]?.ToString() ?? "未知错误";
                        return $"{{\"success\":0,\"reason\":\"获取二维码图片失败: {reason}\"}}";
                    }
                    catch
                    {
                        return $"{{\"success\":0,\"reason\":\"获取二维码图片失败，Content-Type: {contentType}\"}}";
                    }
                }

                // 3. 处理图片流
                using (var stream = response.Content.ReadAsStreamAsync().Result)
                using (var image = Image.FromStream(stream))
                using (var bitmap = new Bitmap(image))
                {
                    // 3.1 检查尺寸
                    if (bitmap.Width != 390 || bitmap.Height != 390)
                    {
                        return "{\"success\":0,\"reason\":\"二维码图片尺寸不符合要求\"}";
                    }

                    // 3.2 检查颜色纯度（仅允许纯黑/纯白）
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            Color pixel = bitmap.GetPixel(x, y);
                            if (!(pixel.R == 0 && pixel.G == 0 && pixel.B == 0) &&
                                !(pixel.R == 255 && pixel.G == 255 && pixel.B == 255))
                            {
                                return "{\"success\":0,\"reason\":\"二维码图片包含非黑白颜色\"}";
                            }
                        }
                    }

                    // 3.3 跳过10像素边框，解析内部 37×37 个方块（每块10×10像素）
                    const int blockSize = 10;
                    const int borderWidth = 10;       // 边框宽度
                    const int blocksPerRow = 37;      // 内部方块列数
                    const int blocksPerCol = 37;      // 内部方块行数

                    var qrLines = new List<string>();
                    for (int blockY = 0; blockY < blocksPerCol; blockY++)
                    {
                        var lineBuilder = new StringBuilder();
                        for (int blockX = 0; blockX < blocksPerRow; blockX++)
                        {
                            int pixelX = borderWidth + blockX * blockSize;
                            int pixelY = borderWidth + blockY * blockSize;
                            Color pixel = bitmap.GetPixel(pixelX, pixelY);
                            lineBuilder.Append(pixel.R == 0 && pixel.G == 0 && pixel.B == 0 ? "██" : "  ");
                        }
                        qrLines.Add(lineBuilder.ToString());
                    }

                    string qrCodeText = string.Join("\n", qrLines);

                    var result = new JObject
                    {
                        ["success"] = 1,
                        ["uuid"] = uuid,
                        ["qrcode"] = qrCodeText
                    };
                    return result.ToString(Formatting.None);
                }
            }
        }
        catch (Exception ex)
        {
            Function.ClientError($"[GetQRCode] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string CheckQRCodeStatus(string uuid, int status)
    {
        try
        {
            // 初始化HTTP
            HttpHelper.InitHttp();

            if (string.IsNullOrEmpty(uuid))
            {
                return "{\"success\":0,\"reason\":\"uuid不能为空\"}";
            }

            // 根据status构造不同的URL
            string url;
            if (status == 2)
            {
                // status为2时，不需要curr_status参数
                url = $"https://service.mkey.163.com/mpay/api/qrcode/query?cv=c0.0.0&game_id=aecglf6ee4aaaarz-g-a50&uuid={Uri.EscapeDataString(uuid)}";
            }
            else if (status == 0 || status == 1)
            {
                // status为0或1时，需要curr_status参数
                url = $"https://service.mkey.163.com/mpay/api/qrcode/query?curr_status={status}&cv=c0.0.0&game_id=aecglf6ee4aaaarz-g-a50&uuid={Uri.EscapeDataString(uuid)}";
            }
            else
            {
                return "{\"success\":0,\"reason\":\"status不合法\"}";
            }

            Function.ClientLog($"[CheckQRCodeStatus] 查询状态URL: {url}");

            // 发送GET请求
            string response = HttpHelper.getAPImclobtPE(url);

            if (string.IsNullOrEmpty(response))
            {
                return "{\"success\":0,\"reason\":\"查询状态请求无响应\"}";
            }

            Function.ClientLog($"[CheckQRCodeStatus] 查询状态响应: {response}");

            // 解析响应
            JObject responseJson;
            try
            {
                responseJson = JObject.Parse(response);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"查询状态响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查是否有错误信息
            if (responseJson["code"] != null)
            {
                string errorReason = responseJson["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"查询状态失败: {errorReason}\"}}";
            }

            // 检查qrcode字段是否存在
            JToken qrcodeToken = responseJson["qrcode"];
            if (qrcodeToken == null)
            {
                return "{\"success\":0,\"reason\":\"响应中缺少qrcode字段\"}";
            }

            // 获取status字段
            int qrcodeStatus = qrcodeToken["status"]?.ToObject<int>() ?? -1;

            // 根据不同的status返回不同的结果
            if (qrcodeStatus == 0 || qrcodeStatus == 1)
            {
                // status为0或1，返回success:2
                return $"{{\"success\":2,\"status\":{qrcodeStatus}}}";
            }
            else if (qrcodeStatus == 2)
            {
                // status为2，检查是否有login_info字段
                JToken loginInfoToken = responseJson["login_info"];
                if (loginInfoToken == null)
                {
                    return "{\"success\":0,\"reason\":\"状态为2但缺少login_info字段\"}";
                }

                string code = loginInfoToken["code"]?.ToString();
                string loginChannel = loginInfoToken["login_channel"]?.ToString();

                if (string.IsNullOrEmpty(code))
                {
                    return "{\"success\":0,\"reason\":\"login_info中缺少code字段\"}";
                }

                if (string.IsNullOrEmpty(loginChannel))
                {
                    loginChannel = "netease"; // 如果没有login_channel，默认为netease
                }

                return $"{{\"success\":1,\"code\":\"{code}\",\"login_channel\":\"{loginChannel}\"}}";
            }
            else
            {
                // 其他未知状态
                return $"{{\"success\":0,\"reason\":\"未知的二维码状态: {qrcodeStatus}\"}}";
            }
        }
        catch (Exception ex)
        {
            Function.ClientError($"[CheckQRCodeStatus] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string SendSMS(string nationcode, string phonenumber, string urs_udid, string mcount_transaction_id, string process_id)
    {
        try
        {
            HttpHelper.InitHttp();
            // 检查MPayDeviceInfo是否存在
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();

            if (string.IsNullOrEmpty(deviceId))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息中缺少device_id\"}";
            }

            // 构造mobile参数
            string mobile = $"{nationcode}-{phonenumber}";

            // 构造参数字符串
            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=a50_sdk_cn&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x32&");
            parameters.Append($"cv=a5.9.0&");
            parameters.Append($"device_id={Uri.EscapeDataString(deviceId)}&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.0.0.0&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={Uri.EscapeDataString(mcount_transaction_id)}&");
            parameters.Append($"mobile={Uri.EscapeDataString(mobile)}&");
            parameters.Append($"process_id={Uri.EscapeDataString(process_id)}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"updater_cv=c1.0.0&");
            parameters.Append($"urs_udid={Uri.EscapeDataString(urs_udid)}");


            // 发送请求
            string url = "https://service.mkey.163.com/mpay/api/users/login/mobile/get_sms";
            Function.ClientLog($"[SendSMS] 请求URL: {url}");
            Function.ClientLog($"[SendSMS] 请求参数: {parameters}");

            string response = HttpHelper.postAPImclobtPE(url, parameters.ToString());
            Function.ClientLog($"[SendSMS] 响应: {response}");

            if (string.IsNullOrEmpty(response))
            {
                return "{\"success\":0,\"reason\":\"服务器无响应\"}";
            }

            // 解析响应
            JObject responseObj;
            try
            {
                responseObj = JObject.Parse(response);
            }
            catch
            {
                // 如果解析失败，可能是空对象或其他格式
                if (response.Trim() == "{}")
                {
                    return "{\"success\":1}";
                }
                else
                {
                    return $"{{\"success\":0,\"reason\":\"响应格式不正确: {response}\"}}";
                }
            }

            // 检查是否有错误信息
            if (responseObj["code"] != null)
            {
                string errorReason = responseObj["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorReason}\"}}";
            }

            // 如果响应为空对象，则视为成功
            if (response.Trim() == "{}")
            {
                return "{\"success\":1}";
            }

            // 其他情况视为成功
            return "{\"success\":1}";
        }
        catch (Exception ex)
        {
            Function.ClientError($"[SendSMS] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }
    public static string VerifySMS(int verifycode, string nationcode, string phonenumber, string urs_udid, string mcount_transaction_id, string process_id)
    {
        try
        {
            // 检查MPayDeviceInfo是否存在
            if (string.IsNullOrEmpty(app.Temp.MpayDeviceInfo))
            {
                NewMPayDeviceInfo();
            }

            // 解析MpayDeviceInfo
            string decodedJson = Encoding.UTF8.GetString(Convert.FromBase64String(app.Temp.MpayDeviceInfo));
            JObject deviceInfo = JObject.Parse(decodedJson);

            string deviceId = deviceInfo["id"]?.ToString();

            if (string.IsNullOrEmpty(deviceId))
            {
                return "{\"success\":0,\"reason\":\"MPay设备信息中缺少device_id\"}";
            }

            // 构造mobile参数
            string mobile = $"{nationcode}-{phonenumber}";

            // 构造参数字符串
            StringBuilder parameters = new StringBuilder();
            parameters.Append($"app_channel=a50_sdk_cn&");
            parameters.Append($"app_mode=2&");
            parameters.Append($"app_type=games&");
            parameters.Append($"arch=win_x32&");
            parameters.Append($"cv=a5.9.0&");
            parameters.Append($"device_id={Uri.EscapeDataString(deviceId)}&");
            parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
            parameters.Append($"gv=1.0.0.0&");
            parameters.Append($"login_for=1&");
            parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
            parameters.Append($"mcount_transaction_id={Uri.EscapeDataString(mcount_transaction_id)}&");
            parameters.Append($"mobile={Uri.EscapeDataString(mobile)}&");
            parameters.Append($"process_id={Uri.EscapeDataString(process_id)}&");
            parameters.Append($"smscode={verifycode}&");
            parameters.Append($"sv=10.0.26200&");
            parameters.Append($"updater_cv=c1.0.0&");
            parameters.Append($"urs_udid={Uri.EscapeDataString(urs_udid)}");

            // 初始化HTTP
            HttpHelper.InitHttp();

            // 发送请求
            string url = "https://service.mkey.163.com/mpay/api/users/login/mobile/verify_sms";
            Function.ClientLog($"[VerifySMS] 请求URL: {url}");
            Function.ClientLog($"[VerifySMS] 请求参数: {parameters}");

            string response = HttpHelper.postAPImclobtPE(url, parameters.ToString());
            Function.ClientLog($"[VerifySMS] 响应: {response}");

            if (string.IsNullOrEmpty(response))
            {
                return "{\"success\":0,\"reason\":\"服务器无响应\"}";
            }

            // 解析响应
            JObject responseObj;
            try
            {
                responseObj = JObject.Parse(response);
            }
            catch (Exception jsonEx)
            {
                return $"{{\"success\":0,\"reason\":\"响应JSON解析失败: {jsonEx.Message}\"}}";
            }

            // 检查是否有错误信息
            if (responseObj["code"] != null)
            {
                string errorReason = responseObj["reason"]?.ToString() ?? "未知错误";
                return $"{{\"success\":0,\"reason\":\"{errorReason}\"}}";
            }

            // 检查是否有ticket字段
            string ticket = responseObj["ticket"]?.ToString();
            if (!string.IsNullOrEmpty(ticket))
            {
                return $"{{\"success\":1,\"ticket\":\"{ticket}\"}}";
            }

            // 如果没有ticket，检查是否有其他可能的成功标识
            if (responseObj["guide_text"] != null)
            {
                // 即使有guide_text，如果没有ticket，也不算是完整成功
                return "{\"success\":0,\"reason\":\"响应中缺少ticket字段\"}";
            }

            // 其他未知响应格式
            return $"{{\"success\":0,\"reason\":\"未知响应格式: {response}\"}}";
        }
        catch (Exception ex)
        {
            Function.ClientError($"[VerifySMS] 错误: {ex.Message}");
            return $"{{\"success\":0,\"reason\":\"内部错误: {ex.Message}\"}}";
        }
    }

    public static string EncodeMd5(this string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                sb.Append(hashBytes[i].ToString("x2"));
            }
            return sb.ToString();
        }
    }
    public static byte[] EncodeAes(this string input, byte[] key)
    {
        using Aes aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
    }
    public static string EncodeHex(this byte[] input)
    {
        return BitConverter.ToString(input).Replace("-", "").ToLowerInvariant();
    }
    public static byte[] DecodeHex(this string input)
    {
        if (input.Length % 2 != 0)
            throw new ArgumentException("输入字符串长度必须是偶数");

        int numberChars = input.Length;
        byte[] bytes = new byte[numberChars / 2];

        for (int i = 0; i < numberChars; i += 2)
        {
            bytes[i / 2] = Convert.ToByte(input.Substring(i, 2), 16);
        }

        return bytes;
    }
    public static string GenerateParameters()
    {
        var parameters = new StringBuilder();
        string deviceName = GenerateDeviceName();
        string macAddress = GenerateMacAddress();
        int processId = _random.Next(100, 65536);
        string udid = GenerateHexString(32, true);
        string uniqueId = GenerateHexString(32, false);

        parameters.Append($"app_channel=a50_sdk_cn&");
        parameters.Append($"app_mode=2&");
        parameters.Append($"app_type=games&");
        parameters.Append($"arch=win_x32&");
        parameters.Append($"brand=Microsoft&");
        parameters.Append($"cv=a5.9.0&");
        parameters.Append($"device_model=pc_mode&");
        parameters.Append($"device_name={deviceName}&");
        parameters.Append($"device_type=Computer&");
        parameters.Append($"game_id=aecfrxodyqaaaajp-g-x19&");
        parameters.Append($"gv=1.0.0.0&");
        parameters.Append($"init_urs_device=0&");
        parameters.Append($"mac={macAddress}&");
        parameters.Append($"mcount_app_key=EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq&");
        parameters.Append($"process_id={processId}&");
        parameters.Append($"resolution=2560%2A1600&");
        parameters.Append($"sv=10.0.26200&");
        parameters.Append($"system_name=windows&");
        parameters.Append($"system_version=10.0.26200&");
        parameters.Append($"udid={udid}&");
        parameters.Append($"unique_id={uniqueId}&");
        parameters.Append($"updater_cv=c1.0.0");

        return parameters.ToString();
    }

    private static string GenerateMacAddress()
    {
        string hexString = GenerateHexString(12, true);
        StringBuilder mac = new StringBuilder();
        for (int i = 0; i < hexString.Length; i += 2)
        {
            if (i > 0) mac.Append("-");
            mac.Append(hexString.Substring(i, 2));
        }

        return mac.ToString();
    }

    private static string GenerateHexString(int length, bool uppercase)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] bytes = new byte[length / 2 + (length % 2)];
            rng.GetBytes(bytes);

            StringBuilder hex = new StringBuilder();
            foreach (byte b in bytes)
            {
                hex.Append(b.ToString(uppercase ? "X2" : "x2"));
            }
            if (length % 2 == 1)
            {
                hex.Length = hex.Length - 1;
            }
            return hex.ToString().Substring(0, length);
        }
    }

    private static string GenerateDeviceName()
    {
        string prefix;
        int randomPartLength;
        if (_random.Next(0, 2) == 0)
        {
            prefix = "DESKTOP-";
            randomPartLength = 7;
        }
        else
        {
            prefix = "LAPTOP-";
            randomPartLength = 8;
        }
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string randomPart = new string(Enumerable.Repeat(chars, randomPartLength)
            .Select(s => s[_random.Next(s.Length)]).ToArray());

        return prefix + randomPart;
    }
}
