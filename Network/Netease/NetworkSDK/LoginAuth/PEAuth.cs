using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.Network.Netease.NetworkSDK.PCCommon;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.SDK.Native;
using Mark;
using Noya.LocalServer.Common.Extensions;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Exceptions;
using PLDUOBAO.Util;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth
{
    internal class PEAuth
    {
        // 表示登录结果的状态枚举，具体包含：
        // LoginState.Success - 登录成功
        // LoginState.TokenExpired - Cookie过期，一般来说是渠道服特有特性
        // LoginState.AccountBanned - 账号被封禁，可能是被连锁了
        // LoginState.InvaildCookie - 无效的 Cookie，说明你的Cookie格式不对/太老了
        // LoginState.InvaildDevice - 非法设备，常被判定为"服务器维护"，实际上得看subcode和subMessage
        // LoginState.Failed - 登录失败
        public enum LoginState
        {
            Success,
            TokenExpired,
            AccountBanned,
            InvaildCookie,
            InvaildDevice,
            Failed,
        }
        public static LoginSercurity GetLoginSercurity()
        {
            //Encrypto.Decrypt_B64();
            LoginSercurity ls = new LoginSercurity();
            //ls.PE_url = "https://g79obtcore.nie.netease.com:8443";
            ls.PE_url = "https://g79obtapigtcoregray.minecraft.cn";
            //ls.engineVersion = "3.4.5.272725";
            //ls.patchVersion = "3.4.118.273808";
            //ls.libminecraftpe = "8496013eea6d450019160b20adc7c124";
            //ls.patch = "88e37c4f509cc41b29469c983c0dee9a2b3e7ca013bb30a74d822579860c042b";

            //ls.engineVersion = "3.9.0.296599";
            //ls.patchVersion = "3.9.3.296713";
            //ls.libminecraftpe = "ab180d3f154e83d370367f026585a0c1";
            //ls.patch = "b96df71d3bbbf810520f6694a075141c";
            //ls.apkSignHash = "2b3e7ca013bb30a74d822579860c042b";
            //ls.offset = 3;
            //ls.rounds = 6;

            ls.engineVersion = VerificationProfile.EngineVersion;
            ls.patchVersion = VerificationProfile.PatchVersion;
            ls.libminecraftpe = VerificationProfile.LibMinecraftpeSoHash;
            ls.patch = VerificationProfile.PatchHash;
            ls.apkSignHash = VerificationProfile.ApkSignHash;
            ls.offset = VerificationProfile.SignIndex;
            ls.rounds = VerificationProfile.SignLoopTimes;

            return ls;
        }

        // 进行Netease G79 Protocal PE-Authentictaion验证
        // sauth: 可用的Cookie
        // 登录状态，如果为 LoginState.Success 则为成功，其他查看对应LoginState说明。
        // ArgumentNullException: Cookie为空或Cookie为不可见字符
        // ArgumentException: Cookie有毛病，一般是Cookie解析失败
        // Exception: 其他错误
        // HttpRequestException: 网爆炸了会遭遇的情况，很少见
        public static async Task<LoginState> Login(string sauth)
        {
            if (string.IsNullOrWhiteSpace(sauth))
            {
                throw new ArgumentNullException(nameof(sauth),"Login Cookie couldn't be null.");
            }


            SercurityAuth? sauth_json = null;

            try
            {
                sauth_json = JsonSerializer.Deserialize(sauth, SercurityAuthContext.Default.SercurityAuth);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invaild Sauth(Cookie),{ex.Message}");
            }

            string guid = Guid.NewGuid().ToString("D");
            // TODO: LoginSercuity
            LoginSercurity? ls = GetLoginSercurity();
            if (ls == null)
            {
                throw new Exception("Failed on get login Sercurity.");
            }

            string LoginMsg = $"{ls.engineVersion}{ls.libminecraftpe}{ls.patchVersion}{ls.patch}{ls.apkSignHash}{guid}";//ls.engineVersion + ls.libminecraftpe + ls.patchVersion + ls.patch + guid;
            byte[]? SignBytes = CoreSign.PESignCount(LoginMsg,ls.offset,ls.rounds);
            if (SignBytes == null)
            {
                throw new Exception("Failed on Calc Sign.");
            }

            string Sign = Convert.ToBase64String(SignBytes);

            //return PEAuth.LoginState.Failed;


            string LoginContent = JsonSerializer.Serialize(new PEAuthentication()
            {
                sa_data = PESADataGenerator.GenerateSaDataPE(),
                engine_version = ls.engineVersion,
                patch_version = ls.patchVersion,
                message = LoginMsg,
                sauth_json = sauth_json?.sauth_json,
                seed = guid,
                sign = Sign,
            }
            , PEAuthenticationContext.Default.PEAuthentication);
            // LoginContent 含 sa_data / sauth_json / sign，脱敏后再打印
            LogSys.Log($"Login Content: {LogRedactor.Redact(LoginContent)}");

            string encryptedText = x19Crypt.HttpEncrypt_g79v12(Encoding.UTF8.GetBytes(LoginContent)).ToHex();
            using var stringContent = new StringContent(encryptedText, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(ls.PE_url + "/pe-authentication", stringContent);
            response.EnsureSuccessStatusCode(); // 推荐添加，自动抛出异常以便捕获请求失败

            string encryptedResponse = await response.Content.ReadAsStringAsync();
            string loginOtpContent = x19Crypt.HttpDecrypt_g79v12_(encryptedResponse.HexToBytes());

            LogSys.Log(loginOtpContent);

            ApiResponse<AuthcationResponseEntity>? opt = null;
            try
            {
                opt = JsonSerializer.Deserialize(loginOtpContent, ApiResponseContext.Default.ApiResponseAuthcationResponseEntity);

            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed Prasing LoginOpt,{ex.Message}");
            }

            if (opt == null)
            {
                return LoginState.Failed;
            }

            NetworkLocalAPI.CurrentAvaliableUser = opt.entity;
            NetworkLocalAPI.CurrentSAuth = sauth;
            NetworkLocalAPI.CurrentUserLoginTime = DateTimeOffset.Now;

            if (NetworkLocalAPI.CurrentAvaliableUser != null)
            {
                //这里启动了Token刷新进程，不用管，如果退出登录了他会自己销毁
                var update = new UpdateAuthenticationService(NetworkLocalAPI.CurrentAvaliableUser);
            }

            switch (opt.code)
            {
                case 0:
                    return LoginState.Success;
                case 4:
                    return LoginState.InvaildCookie;
                case 8:
                    return LoginState.AccountBanned;
                case 32:
                    ApiResponse<LoginFailedEntity>? optFailed = null;
                    try
                    {
                        optFailed = JsonSerializer.Deserialize(loginOtpContent, ApiResponseContext.Default.ApiResponseLoginFailedEntity);

                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Failed Prasing LoginOpt,{ex.Message}");
                    }

                    if (optFailed is null)
                        throw new ArgumentException($"Failed Prasing LoginOpt");

                    if (optFailed.entity is null)
                        throw new ArgumentException($"Failed Prasing LoginOpt, entity is null");

                    throw new LoginFailedException(optFailed.entity);
                case 2001:
                    // 服务端把这个码叫「客户端版本过低」，实测成因有三类：
                    //   1) VerificationProfile 的渠道指纹与登录渠道不匹配（so hash 用错包）
                    //   2) sauth 里缺网易侧会话（没跑过 mgbsdk uni_sauth 交换）
                    //   3) message 拼接内容与服务端算出的不一致（sign 对不上）
                    // 打出当时的完整上下文，便于定位。
                    LogSys.Warn("[PEAuth] code 2001 客户端版本异常");
                    LogSys.Warn($"  engine={ls.engineVersion}");
                    LogSys.Warn($"  soHash={ls.libminecraftpe}");
                    LogSys.Warn($"  patchVersion={ls.patchVersion} patchHash={ls.patch}");
                    LogSys.Warn($"  apkSign={ls.apkSignHash} offset={ls.offset} rounds={ls.rounds}");
                    var sj = sauth_json?.GetSAuthJson();
                    if (sj != null)
                    {
                        LogSys.Warn($"  sauth.login_channel={sj.login_channel} app_channel={sj.app_channel} " +
                                    $"sdk_version={sj.sdk_version}");
                        LogSys.Warn($"  sauth.aid={sj.aid} has_access_token={!string.IsNullOrEmpty(sj.access_token)} " +
                                    $"has_sign={!string.IsNullOrEmpty(sj.sign)}");
                        LogSys.Warn($"  sauth.sessionid={(sj.sessionid?.Length > 40 ? sj.sessionid[..40] + "..." : sj.sessionid)}");
                    }
                    else
                    {
                        LogSys.Warn("  sauth 解析失败（sauth_json 不是合法 JSON）");
                    }
                    return LoginState.InvaildDevice;
                default:
                    throw new HttpRequestException($"错误信息：{opt.message}，状态码：{opt.code}");
            }
           
        }
    }
}
