using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml.Templates;
using Elysium.Core.Utils;
using PLDUOBAO.Network;
using PLDUOBAO.Network.NewVerify;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Util
{
    internal class PathUtils
    {
        public static string AppCurrentPath => AppContext.BaseDirectory;
        
        /// <summary>
        /// PL 配置文件目录: %APPDATA%\.pl
        /// </summary>
        public static string ConfigPath
        {
            get
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".pl");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }
        
        public static string SpoofTool64Path => Path.Combine(AppCurrentPath, "Native", "PLDUOBAO.DeviceSpoof64.exe");
        public static string PyCodeExecuter64Path => Path.Combine(AppCurrentPath, "Native", "PyCodeExecuter64.dll");
        public static string LaunchConfigPath => Path.Combine(AppCurrentPath, "launch.cppconfig");
        
        /// <summary>MPay / 4399 等 .cds 设备文件目录（%APPDATA%\.pl\cds）</summary>
        public static string CdsStorePath
        {
            get
            {
                string path = Path.Combine(ConfigPath, "cds");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        // 以下配置文件存储到 .pl 目录
        public static string UserConfigPath => Path.Combine(ConfigPath, "userconfig.ini");
        public static string TempAccountFilePath => Path.Combine(ConfigPath, "options.ini");
        public static string AccountListFilePath => Path.Combine(ConfigPath, "accountlist");

        public static string ProgramCachePath
        {
            get
            {
                string path = Path.Combine(Path.GetTempPath(), "GFJCache");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        public static string ServerIconCachePath
        {
            get
            {
                string path = Path.Combine(ProgramCachePath, "ServerIcons");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        private static bool IsServerIconExist(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                return Path.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static string GetServerIconPath(string itemId)
        {
            if (string.IsNullOrWhiteSpace(ServerIconCachePath))
                return string.Empty;

            try
            {
                return Path.Combine(ServerIconCachePath, $"{itemId}.png");
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async Task<Stream?> LoadServerIconAsync(UCServerBar sb)
        {
            try
            {
                // sb 本身为空
                if (sb == null)
                    return null;

                // ngi 为空
                if (sb.ngi == null)
                    return null;

                // ServerIconCachePath 未初始化
                if (string.IsNullOrWhiteSpace(ServerIconCachePath))
                {
                    LogSys.Err("LoadServerIconAsync: ServerIconCachePath is null or empty.");
                    return null;
                }

                // ItemId 为空
                if (string.IsNullOrWhiteSpace(sb.ngi.ItemId))
                {
                    LogSys.Err("LoadServerIconAsync: ItemId is null or empty.");
                    return null;
                }

                string iconPath = GetServerIconPath(sb.ngi.ItemId);

                // 如果图标文件已存在
                if (IsServerIconExist(iconPath))
                {
                    try
                    {
                        return File.OpenRead(iconPath);
                    }
                    catch (Exception ex)
                    {
                        LogSys.Err($"LoadServerIconAsync: Failed to open file {iconPath}. {ex}");
                        return null;
                    }
                }

                // 下载新图标
                if (string.IsNullOrWhiteSpace(sb.ngi.TitleImageUrl))
                {
                    LogSys.Err("LoadServerIconAsync: TitleImageUrl is null or empty, cannot download.");
                    return null;
                }

                try
                {
                    await FileDownloader.DownloadFileAsync(sb.ngi.TitleImageUrl, iconPath);
                }
                catch (Exception ex)
                {
                    LogSys.Err($"LoadServerIconAsync: Download failed. {ex}");
                    return null;
                }

                // 下载成功后再尝试加载
                return await LoadServerIconAsync(sb);
            }
            catch (Exception ex)
            {
                LogSys.Err($"LoadServerIconAsync: Unexpected error. {ex}");
                return null;
            }
        }

        public static string AccountListPath
        {
            get 
            {
                string path = Path.Combine(ConfigPath, "user");
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        public static bool CreateNewCookie(string Sauth,string FilePath)
        {
            try
            {
                string path = Path.Combine(AccountListPath,FilePath + ".json");
                File.WriteAllText(path,Sauth);
                return true;
            }
            catch (Exception ex)
            {
                LogSys.Err(ex);
                return false;
            }
            
        }

        private static readonly object _accountFileLock = new();

        public static bool SaveAccount(AccountSystem.Account acc)
        {
            try
            {
                var payload = Encrypto.Encrypt(JsonSerializer.Serialize(AccountSystem.user, AccContext.Default.Account));
                lock (_accountFileLock)
                {
                    var tmp = TempAccountFilePath + ".tmp";
                    File.WriteAllText(tmp, payload, Encoding.UTF8);
                    if (File.Exists(TempAccountFilePath))
                        File.Replace(tmp, TempAccountFilePath, null);
                    else
                        File.Move(tmp, TempAccountFilePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                LogSys.Err($"PathUtil::SaveAccount {ex}");
                return false;
            }
        }

        public static AccountSystem.Account? LoadAccount()
        {
            if (!Path.Exists(TempAccountFilePath))
            {
                return null;
            }

            // 最多重试几次, 防止刚好撞上写入.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    string data;
                    lock (_accountFileLock)
                    {
                        using var fs = new FileStream(
                            TempAccountFilePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite);
                        using var sr = new StreamReader(fs, Encoding.UTF8);
                        data = sr.ReadToEnd();
                    }

                    data = Encrypto.Decrypt(data);
                    return JsonSerializer.Deserialize(data, AccContext.Default.Account);
                }
                catch (IOException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(30);
                }
                catch (Exception ex)
                {
                    LogSys.Err($"PathUtil::LoadAccount {ex}");
                    return null;
                }
            }

            return null;
        }


        private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

        private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };

        /// <summary>
        /// 是否是合法的 Windows 文件名（不包含路径），不包含重名检查
        /// </summary>
        private static bool IsValidWindowsFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (fileName == "." || fileName == "..")
                return false;

            if (fileName.IndexOfAny(InvalidChars) >= 0)
                return false;

            if (fileName.EndsWith(" ") || fileName.EndsWith("."))
                return false;

            var nameWithoutExtension = fileName.Split('.')[0];
            nameWithoutExtension = nameWithoutExtension.TrimEnd(' ', '.');

            if (string.IsNullOrWhiteSpace(nameWithoutExtension))
                return false;

            if (ReservedNames.Contains(nameWithoutExtension, StringComparer.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// 检查在给定路径下，这个名字能否用来新建文件/文件夹
        /// </summary>
        /// <param name="directoryPath">目标目录</param>
        /// <param name="fileName">要检查的文件名</param>
        /// <returns>true=可用，false=不可用</returns>
        public static bool CanUseFileName(string directoryPath, string fileName)
        {
            // 首先检查文件名本身是否合法
            if (!IsValidWindowsFileName(fileName))
                return false;

            try
            {
                // 合并成完整路径
                var fullPath = Path.Combine(directoryPath, fileName);

                // 检查是否已经存在同名文件或文件夹
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    return false;
            }
            catch
            {
                // 如果组合路径时出错，也视为不可用
                return false;
            }

            return true;
        }
        
        public static UserConfig? LoadConfig()
        {
            try
            {
                if (!File.Exists(UserConfigPath))
                    return new UserConfig();

                string json = File.ReadAllText(UserConfigPath);
                var cfg = JsonSerializer.Deserialize(json, UserConfigContext.Default.UserConfig);
                if (cfg == null) return new UserConfig();

                bool scrubbed = false;
                if (!string.IsNullOrWhiteSpace(cfg.userGamePath) && CJKPathGuard.ContainsCJK(cfg.userGamePath))
                {
                    LogSys.Warn($"[PathUtils] userGamePath 含 CJK 字符，已清空：{cfg.userGamePath}");
                    cfg.userGamePath = null;
                    scrubbed = true;
                }
                if (!string.IsNullOrWhiteSpace(cfg.userSkinPath) && CJKPathGuard.ContainsCJK(cfg.userSkinPath))
                {
                    LogSys.Warn($"[PathUtils] userSkinPath 含 CJK 字符，已清空：{cfg.userSkinPath}");
                    cfg.userSkinPath = null;
                    scrubbed = true;
                }
                if (!string.IsNullOrWhiteSpace(cfg.gameDownloadPath) && CJKPathGuard.ContainsCJK(cfg.gameDownloadPath))
                {
                    LogSys.Warn($"[PathUtils] gameDownloadPath 含 CJK 字符，已清空：{cfg.gameDownloadPath}");
                    cfg.gameDownloadPath = null;
                    scrubbed = true;
                }
                if (cfg.skinHistory != null)
                {
                    int before = cfg.skinHistory.Count;
                    cfg.skinHistory.RemoveAll(h => !string.IsNullOrWhiteSpace(h.Path) && CJKPathGuard.ContainsCJK(h.Path));
                    if (cfg.skinHistory.Count != before) scrubbed = true;
                }
                if (scrubbed) SaveConfig(cfg);
                return cfg;
            }
            catch (Exception ex)
            {
                LogSys.Err($"PathUtils::LoadConfig {ex}");
                return new UserConfig();
            }
        }
        
        /// <summary>
        /// 保存用户配置
        /// </summary>
        public static bool SaveConfig(UserConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, UserConfigContext.Default.UserConfig);
                File.WriteAllText(UserConfigPath, json);
                return true;
            }
            catch (Exception ex)
            {
                LogSys.Err($"PathUtils::SaveConfig {ex}");
                return false;
            }
        }

    }
}
