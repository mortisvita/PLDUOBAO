using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PLDUOBAO.Network.Netease.NetworkSDK
{
    // 检测 games\com.netease 下已解包/已解密的服务器资源包，确定 cppconfig 该用的 level_id。
    // 背景：游戏按 cppconfig 的 level_id 去找
    // games\com.netease\resource_packs\{level_id}_resource_packs_*。
    // 用错 ID 会导致游戏找不到资源包，表现为方块紫黑格子、服务器自定义模型不显示。
    // 注意：packcache 里的包是加密的（文件头 90 1D 30 01 + GUID），
    // 直接部署会因为无法解码而仍然显示紫黑格子。这里只识别已解密的目录。
    internal static class ServerPackScanner
    {
        private static string NeteaseRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "MinecraftPE_Netease");

        private static string GamesRoot =>
            Path.Combine(NeteaseRoot, "games", "com.netease");

        // PNG 文件头，用于判定资源包是否已解密。
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47 };

        // 选择与已解包资源包匹配的世界 ID。
        // 优先 preferred: （若其资源包目录存在且已解密），
        // 否则返回已解密资源包最多的那个 ID；都没有时返回 preferred。
        public static string ResolveWorldId(string preferred = "114514")
        {
            try
            {
                List<string> usable = EnumerateUsableOwners();
                if (usable.Count == 0)
                    return preferred;

                if (!string.IsNullOrEmpty(preferred)
                    && usable.Contains(preferred, StringComparer.OrdinalIgnoreCase))
                {
                    return preferred;
                }

                string best = preferred;
                int bestCount = -1;
                foreach (string id in usable)
                {
                    int count = CountPacks("resource_packs", id) + CountPacks("behavior_packs", id);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        best = id;
                    }
                }
                return best;
            }
            catch
            {
                return preferred;
            }
        }

        // 枚举"已解密"的世界 ID：目录前缀出现、且该目录下至少有一个合法 PNG。
        // 加密包的 PNG 头不是 89 50 4E 47，会被排除。
        private static List<string> EnumerateUsableOwners()
        {
            HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);

            foreach (string kind in new[] { "resource_packs", "behavior_packs" })
            {
                string root = Path.Combine(GamesRoot, kind);
                if (!Directory.Exists(root))
                    continue;

                foreach (string dir in Directory.EnumerateDirectories(root))
                {
                    string name = Path.GetFileName(dir);
                    int underscore = name.IndexOf('_');
                    if (underscore <= 0)
                        continue;

                    string owner = name.Substring(0, underscore);
                    if (ids.Contains(owner))
                        continue;

                    if (HasDecryptedPng(dir))
                        ids.Add(owner);
                }
            }

            return ids.ToList();
        }

        // 目录内是否存在至少一个文件头合法的 PNG。
        private static bool HasDecryptedPng(string dir)
        {
            try
            {
                foreach (string png in Directory.EnumerateFiles(dir, "*.png", SearchOption.AllDirectories))
                {
                    using FileStream fs = File.OpenRead(png);
                    byte[] head = new byte[4];
                    if (fs.Read(head, 0, 4) == 4
                        && head[0] == PngSignature[0]
                        && head[1] == PngSignature[1]
                        && head[2] == PngSignature[2]
                        && head[3] == PngSignature[3])
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // 读不到就当不可用
            }
            return false;
        }

        // 统计某世界 ID 下指定类型、且已解密的资源包目录数量。
        private static int CountPacks(string kind, string worldId)
        {
            string root = Path.Combine(GamesRoot, kind);
            if (!Directory.Exists(root) || string.IsNullOrWhiteSpace(worldId))
                return 0;

            string prefix = worldId + "_";
            int count = 0;
            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                if (!Path.GetFileName(dir).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (HasDecryptedPng(dir))
                    count++;
            }
            return count;
        }
    }
}
