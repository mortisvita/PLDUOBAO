using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;

namespace PLDUOBAO.SDK
{
    [JsonSerializable(typeof(UserCookie))]
    [JsonSerializable(typeof(List<UserCookie>))]
    internal partial class UserCookieContext : JsonSerializerContext
    {

    }
    public class UserCookie
    {
        public enum CookiePlatform
        {
            None,
            Netease,
            com4399,
            localCookie,
        }
        public string Name { get; set; } = "";
        public string SAuth { get; set; } = "";
        public CookiePlatform Platform { get; set; }
        public string Username4399 { get; set; } = "";
        public string Password4399 { get; set; } = "";
        
        // 账号备注/昵称，用于区分多账号
        public string? Nickname { get; set; }

        public UserCookie() { }
        public UserCookie(string Name,string SAuth,CookiePlatform platform = CookiePlatform.localCookie) 
        {
            this.Name = Name;
            this.SAuth = SAuth;
            this.Platform = platform;
            if (platform == CookiePlatform.None)
                throw new ArgumentException("[UserCookie] CookiePlatform is None, this can't be happend.");
        }
        public UserCookie(string Name, string Username,string Password, CookiePlatform platform = CookiePlatform.localCookie)
        {
            this.Name = Name;
            this.Username4399 = Username;
            this.Password4399 = Password;
            this.Platform = platform;
            if (platform == CookiePlatform.None)
                throw new ArgumentException("[UserCookie] CookiePlatform is None, this can't be happend.");
        }
        public override string ToString()
        {
            return $"{Name} —— {Platform}";
        }
    }
}
