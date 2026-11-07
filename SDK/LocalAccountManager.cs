using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease;
using PLDUOBAO.Util;

namespace PLDUOBAO.SDK
{
    internal class LocalAccountManager
    {
        public static string NewUserListPath => PathUtils.AccountListFilePath;

        public static List<UserCookie> Users { get; set; } = new List<UserCookie>();

        public static void SaveCurrentUser()
        {
            if (NetworkLocalAPI.CurrentAvaliableUser == null ||
                NetworkLocalAPI.CurrentPlatform == UserCookie.CookiePlatform.None ||
                string.IsNullOrEmpty(NetworkLocalAPI.CurrentSAuth) ||
                string.IsNullOrEmpty(NetworkLocalAPI.CurrentUserName))
                return;

            if (Users.Any(x => x.Name == NetworkLocalAPI.CurrentUserName))
                return;

            if (NetworkLocalAPI.CurrentPlatform == UserCookie.CookiePlatform.com4399)
                if (string.IsNullOrEmpty(NetworkLocalAPI.Current4399Username) ||
                    string.IsNullOrEmpty(NetworkLocalAPI.Current4399Password))
                    return;

            switch (NetworkLocalAPI.CurrentPlatform)
            {
                case UserCookie.CookiePlatform.None:
                    throw new Exception("我去了，怎么会是None");
                case UserCookie.CookiePlatform.localCookie:
                    Users.Add(new UserCookie(NetworkLocalAPI.CurrentUserName,
                                             NetworkLocalAPI.CurrentSAuth,
                                             UserCookie.CookiePlatform.localCookie));
                    break;
                case UserCookie.CookiePlatform.Netease:
                    Users.Add(new UserCookie(NetworkLocalAPI.CurrentUserName,
                                             NetworkLocalAPI.CurrentSAuth,
                                             UserCookie.CookiePlatform.Netease));
                    break;
                case UserCookie.CookiePlatform.com4399:
                    Users.Add(new UserCookie(NetworkLocalAPI.CurrentUserName,
                                             NetworkLocalAPI.Current4399Username!,
                                             NetworkLocalAPI.Current4399Password!,
                                             UserCookie.CookiePlatform.com4399));
                    break;
            }
            
            

            SaveUsers();
        }

        // 改名后同步更新账号列表里当前登录账号的显示名。
        public static bool UpdateCurrentUserDisplayName(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(newName))
                return false;

            UserCookie? user = null;
            var platform = NetworkLocalAPI.CurrentPlatform;

            if (!string.IsNullOrEmpty(NetworkLocalAPI.CurrentSAuth))
            {
                user = Users.FirstOrDefault(x =>
                    x.Platform == platform &&
                    x.SAuth == NetworkLocalAPI.CurrentSAuth);
            }

            if (user == null &&
                platform == UserCookie.CookiePlatform.com4399 &&
                !string.IsNullOrEmpty(NetworkLocalAPI.Current4399Username))
            {
                user = Users.FirstOrDefault(x =>
                    x.Platform == UserCookie.CookiePlatform.com4399 &&
                    x.Username4399 == NetworkLocalAPI.Current4399Username);
            }

            if (user == null && !string.IsNullOrEmpty(oldName))
            {
                user = Users.FirstOrDefault(x =>
                    x.Platform == platform &&
                    x.Name == oldName);
            }

            if (user == null)
                return false;

            user.Name = newName;
            SaveUsers();
            return true;
        }

        public static void DeleteUser(UserCookie cookie)
        {
            // 使用 Name 和 Platform 来查找匹配的用户
            var toRemove = Users.FirstOrDefault(x => x.Name == cookie.Name && x.Platform == cookie.Platform);
            if (toRemove == null)
                return;

            Users.Remove(toRemove);

            SaveUsers();
        }

        public static void SaveUsers()
        {
            string json = JsonSerializer.Serialize(Users,
                                                   UserCookieContext.Default.ListUserCookie);

            File.WriteAllText(NewUserListPath,json);

            LoadUsers();
        }

    public static void LoadUsers()
    {
        try
        {
            if (!File.Exists(NewUserListPath))
                return;

            string json = File.ReadAllText(NewUserListPath);

            var list = JsonSerializer.Deserialize(json,
                                                  UserCookieContext.Default.ListUserCookie);

            if (list == null)
                return;

            Users = list;

            if (MainWindow.UCCookieSelector == null)
                return;

            MainWindow.UCCookieSelector.ReloadUsers();

        }
        catch (Exception ex)
        {
            LogSys.Err($"[LocalAccountManager] LoadUsers : {ex}");
        }

    }

    // 把传入的账户写入指定路径。失败返回 false。
    public static bool ExportUsers(List<UserCookie> users, string path)
    {
        if (users == null || users.Count == 0) return false;
        try
        {
            var json = JsonSerializer.Serialize(users,
                                                UserCookieContext.Default.ListUserCookie);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            LogSys.Err($"[LocalAccountManager] ExportUsers : {ex}");
            return false;
        }
    }
}
}
