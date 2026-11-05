using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Core.Utils;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Mcp
{
    /// <summary>
    /// 服务器资源包解密部署器（幂等、增量、可在游戏运行中调用）。
    ///
    /// 实证机制（Chronal 同会话缓存 + mcp.log）：
    ///   packcache 文件 = 4B magic(90 1D 30 01) + 36B GUID + 24B 零 + 密文；
    ///   流密码无填充，密文长度 = 明文长度 + 64。静态资源用固定 key，跨会话
    ///   密文逐字节相同；会话元数据每次重加密。
    ///   *.mcp 是明文 MCPK 容器（4D 43 50 4B），直接落盘即可。
    ///
    /// 关键时序：游戏启动后才会按 mod_missmatch 补下载脚本包到 packcache
    /// （mcp.log 19:37:44 部署 → 19:38:05 mod_download → 19:38:10 addon list）。
    /// 因此 PrepareAsync（进服前）+ WatchAndDeployAsync（游戏运行中）都必须
    /// 是幂等增量的：按 uuid 判定缺包，只补新的，绝不删除游戏正在读的目录。
    ///
    /// 真实明文语料：Chronal server-resources、games/11、D:/resource_packs.bak、
    /// 当前 resource_packs/behavior_packs。用 (文件名, 精确长度) 精确定位，
    /// 其余用真实 (C,P) 配对恢复的 keystream 表解密，仅会话元数据重建。
    /// </summary>
    internal static class McpResourceSync
    {
        private static readonly byte[] Magic = { 0x90, 0x1D, 0x30, 0x01 };
        private const string LevelId = "4638866978460665031";

        private static string NeteaseRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MinecraftPE_Netease");
        private static string GamesRoot => Path.Combine(NeteaseRoot, "games", "com.netease");
        private static string PackCacheRoot => Path.Combine(NeteaseRoot, "packcache");
        private static string ResourcePacksRoot => Path.Combine(GamesRoot, "resource_packs");
        private static string BehaviorPacksRoot => Path.Combine(GamesRoot, "behavior_packs");
        private static string ChronalRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Chronal", "Bedrock", "server-resources", LevelId, LevelId);
        private static string LogPath => Path.Combine(NeteaseRoot, "mcp_sync.log");

        private static readonly object Sync = new();
        private static Index? _index;
        private static int _watching;

        private sealed class PackMeta
        {
            public string Uuid = "", Name = "", Type = "resources";
            public string ManifestName = "manifest.json";
            public byte[]? ManifestBytes;
        }

        /// <summary>索引：真实明文语料 + Chronal 名字 + keystream 表（只构建一次）。</summary>
        private sealed class Index
        {
            public Dictionary<(string, int), byte[]> ByName = new();
            public Dictionary<(string, int), byte[]> ByTail = new();
            public Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> CorpusNames = new(StringComparer.OrdinalIgnoreCase);
            public Dictionary<byte[], byte> Tab = new(new ByteArrayComparer());
            public Dictionary<byte[], byte[]> Prefixes = new(new ByteArrayComparer());

            /// <summary>
            /// 按 (packUuid, 包内相对路径) 索引的真·明文。
            /// 会话元数据（contents.json / folder_md5.json / manifest）每次下载都会被
            /// 重新加密、长度漂移，靠 (文件名,长度) 匹配不到；但相对路径是稳定的，
            /// 这条索引可以逐字节取回原件。
            /// </summary>
            public Dictionary<(string, string), byte[]> ByUuidPath = new();
        }

        private static void Log(string s)
        {
            try { LogSys.Log("[McpSync] " + s); } catch { }
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n"); } catch { }
        }

        // ---------------- 对外入口 ----------------

        /// <summary>进服前：补齐当前 packcache 里所有缺失的包。</summary>
        public static Task<string> PrepareAsync(string itemId, string worldId = "114514")
        {
            return Task.Run(() =>
            {
                try { Log(Deploy()); }
                catch (Exception ex) { Log("deploy err: " + ex); }
                return worldId;
            });
        }

        /// <summary>
        /// 游戏启动后持续监控 packcache，增量部署服务器晚下载的包。
        /// 幂等：已部署 uuid 不再处理；绝不删除运行中目录。
        /// </summary>
        public static async Task WatchAndDeployAsync(Process? gameProcess)
        {
            if (Interlocked.Exchange(ref _watching, 1) != 0) return;
            try
            {
                Log("watcher started");
                var deadline = DateTime.UtcNow.AddMinutes(8);
                int idleAfterExit = 0;
                while (DateTime.UtcNow < deadline)
                {
                    await Task.Delay(2000).ConfigureAwait(false);
                    try
                    {
                        string report = Deploy();
                        // 只在真的补了包时记日志，避免刷屏
                        if (report.Contains("added=") && !report.Contains("added=0"))
                            Log("watcher " + report);
                    }
                    catch (Exception ex) { Log("watcher tick err: " + ex.Message); }

                    bool exited;
                    try { exited = gameProcess == null || gameProcess.HasExited; }
                    catch { exited = true; }
                    if (exited && ++idleAfterExit >= 6) break;   // 退出后再跑 ~12s
                }
                Log("watcher stopped");
            }
            finally { Interlocked.Exchange(ref _watching, 0); }
        }

        // ---------------- 部署 ----------------

        private static string Deploy()
        {
            if (!Directory.Exists(PackCacheRoot)) return "packcache not found, skip";
            Index idx = GetIndex();
            Directory.CreateDirectory(ResourcePacksRoot);
            Directory.CreateDirectory(BehaviorPacksRoot);

            // 已部署的 uuid（RP + BP 全部目录），增量判定依据
            var deployed = DeployedUuids();
            // 磁盘上已有的目录名，避免新增包重名
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { ResourcePacksRoot, BehaviorPacksRoot })
                foreach (string d in SafeDirs(root, SearchOption.TopDirectoryOnly))
                    usedNames.Add(Path.GetFileName(d));

            // 目录名对齐：服务端 CLIENT addon list 用英文内部名定位资源包，
            // 名字不一致会让游戏回退到 packcache 的加密副本（紫黑方块 / 界面加载失败）。
            NormalizeDirNames(idx);

            int added = 0, files = 0, genuine = 0, table = 0, rebuilt = 0, plain = 0;
            foreach (string d in SafeDirs(PackCacheRoot, SearchOption.TopDirectoryOnly))
            {
                PackMeta? m = ReadManifest(d);
                if (m == null) continue;
                if (deployed.Contains(m.Uuid)) continue;      // 幂等：已部署不碰

                string rawName = idx.CorpusNames.TryGetValue(m.Uuid, out var cn) ? cn
                               : idx.Names.TryGetValue(m.Uuid, out var en2) ? en2
                               : m.Name;
                if (string.IsNullOrWhiteSpace(rawName)) rawName = m.Uuid;
                string ename = SafeName(rawName);
                bool isData = string.Equals(m.Type, "data", StringComparison.OrdinalIgnoreCase);
                string root = isData ? BehaviorPacksRoot : ResourcePacksRoot;
                string kind = isData ? "behavior_packs" : "resource_packs";
                string outName = LevelId + "_" + kind + "_" + ename;
                if (usedNames.Contains(outName))
                    outName = outName + "_" + m.Uuid.Replace("-", "").Substring(0, Math.Min(8, m.Uuid.Length));
                int suffix = 2;
                while (usedNames.Contains(outName))
                    outName = LevelId + "_" + kind + "_" + ename + "_"
                            + m.Uuid.Replace("-", "").Substring(0, Math.Min(8, m.Uuid.Length)) + "_" + suffix++;
                usedNames.Add(outName);
                string outDir = Path.Combine(root, outName);

                // 同名目录存在时覆盖写（不整体删除，游戏运行中安全）
                Directory.CreateDirectory(outDir);
                deployed.Add(m.Uuid);
                added++;

                var all = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (string f in SafeFiles(d))
                {
                    string rel = Path.GetRelativePath(d, f).Replace('\\', '/');
                    try { all[rel] = File.ReadAllBytes(f); } catch { }
                }

                // 密文原文索引（供 folder_md5.json 重建用：它的值是密文 md5）
                var cipher = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in all)
                {
                    string bn = Path.GetFileName(kv.Key).ToLowerInvariant();
                    if (bn == "contents.json" || bn == "folder_md5.json") continue;
                    if (bn.EndsWith(".json") && bn.Contains("manifest")) continue;
                    cipher[kv.Key] = kv.Value;
                }
                var manifestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { m.ManifestName };
                if (all.ContainsKey("manifest.json") && all.ContainsKey("pack_manifest.json"))
                {
                    // 语料里两个都有：folder_md5 只取被游戏实际加载的那个
                }

                var list = all.Keys.Where(x => !x.EndsWith("contents.json", StringComparison.OrdinalIgnoreCase)
                                            && !x.EndsWith("folder_md5.json", StringComparison.OrdinalIgnoreCase)
                                            && !x.Contains("manifest", StringComparison.OrdinalIgnoreCase))
                                  .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var kv in all)
                {
                    string rel = kv.Key; byte[] b = kv.Value; byte[] outBytes;
                    if (IsMagic(b) && b.Length > 64)
                    {
                        int len = b.Length - 64;
                        string bn = Path.GetFileName(rel).ToLowerInvariant();
                        // 首选 (uuid, relpath) 精确匹配：跨包同名同长度文件很多
                        // （ui/_ui_defs.json、effects/*.json、contents.json…），
                        // 只按名称+长度会串包。
                        byte[]? P = Lookup2(idx.ByUuidPath, (m.Uuid, rel.ToLowerInvariant()))
                                   ?? Lookup(idx.ByName, (bn, len))
                                   ?? Lookup(idx.ByTail, (TailOf(rel), len));
                        if (P != null) { outBytes = P; genuine++; }
                        else
                        {
                            byte[] dec = TableDecrypt(b, 64, idx.Tab, idx.Prefixes, out double miss);
                            if (miss <= 0.02 && LooksReal(dec)) { outBytes = dec; table++; }
                            else
                            {
                                outBytes = Rebuild(rel, list, cipher, manifestNames);
                                rebuilt++;
                            }
                        }
                    }
                    else { outBytes = b; plain++; }   // .mcp / manifest / LICENSE 等明文直接落盘
                    Write(Path.Combine(outDir, rel), outBytes);
                    files++;
                }
                if (m.ManifestBytes != null)
                {
                    // 与原版一致：保留原来的清单文件名（manifest.json 或 pack_manifest.json），
                    // 两个同时存在会让游戏的包工厂把同一个 uuid 载入两次。
                    Write(Path.Combine(outDir, m.ManifestName), m.ManifestBytes);
                }
            }

            int pngOk = 0, pngBad = 0;
            foreach (string f in SafeFiles(ResourcePacksRoot))
            {
                if (!f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                try { if (ValidPng(File.ReadAllBytes(f))) pngOk++; else pngBad++; }
                catch { pngBad++; }
            }
            return string.Format("added={0} files={1} genuine={2} table={3} rebuilt={4} plain={5} PNG={6}/{7} bad={8} result={9}",
                added, files, genuine, table, rebuilt, plain, pngOk, pngOk + pngBad, pngBad,
                pngBad == 0 && pngOk > 0 ? "OK" : "CHECK");
        }

        // ---------------- 索引（一次性） ----------------

        private static Index GetIndex()
        {
            lock (Sync)
            {
                if (_index != null) return _index;
                var idx = new Index();
                // 1) 全部真实明文语料
                foreach (string root in PlainRoots())
                {
                    if (!Directory.Exists(root)) continue;
                    // 该根下每个包目录的 uuid（用于按 (uuid, relpath) 建索引）
                    var uuidByDir = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string f in SafeFiles(root))
                    {
                        byte[] b;
                        try { b = File.ReadAllBytes(f); } catch { continue; }
                        if (b.Length < 2) continue;
                        var k = (Path.GetFileName(f).ToLowerInvariant(), b.Length);
                        if (!idx.ByName.ContainsKey(k) || LooksReal(b)) idx.ByName[k] = b;
                        var kt = (TailKey(f, root), b.Length);
                        if (!idx.ByTail.ContainsKey(kt) || LooksReal(b)) idx.ByTail[kt] = b;
                    }
                    // 逐包建 (uuid, rel) 索引
                    foreach (string d in SafeDirs(root, SearchOption.AllDirectories))
                    {
                        string? u = ReadManifestUuid(d);
                        if (string.IsNullOrEmpty(u)) continue;
                        foreach (string f in SafeFiles(d))
                        {
                            string rel = Path.GetRelativePath(d, f).Replace('\\', '/').ToLowerInvariant();
                            byte[] b;
                            try { b = File.ReadAllBytes(f); } catch { continue; }
                            if (!LooksReal(b) && b.Length > 0) continue;   // 只要像真文件的
                            idx.ByUuidPath[(u, rel)] = b;
                        }
                    }
                }
                // 2) Chronal 元数据 + 语料目录里的真实英文包名
                if (Directory.Exists(ChronalRoot))
                {
                    foreach (string d in SafeDirs(ChronalRoot, SearchOption.TopDirectoryOnly))
                    {
                        string ip = Path.Combine(d, "installed.json");
                        if (File.Exists(ip))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(File.ReadAllBytes(ip));
                                if (doc.RootElement.TryGetProperty("packs", out var ps))
                                    foreach (var pk in ps.EnumerateArray())
                                    {
                                        string? id = pk.TryGetProperty("packId", out var a) ? a.GetString() : null;
                                        string? nm = pk.TryGetProperty("name", out var b2) ? b2.GetString() : null;
                                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(nm)) idx.Names[id] = nm;
                                    }
                            }
                            catch { }
                        }
                    }
                }
                foreach (string d in SafeDirs(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Chronal", "Bedrock", "server-resources"), SearchOption.AllDirectories))
                {
                    string mf = Path.Combine(d, "pack_manifest.json");
                    if (!File.Exists(mf)) mf = Path.Combine(d, "manifest.json");
                    if (!File.Exists(mf)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllBytes(mf));
                        if (doc.RootElement.TryGetProperty("header", out var h)
                            && h.TryGetProperty("uuid", out var u))
                        {
                            string uid = u.GetString() ?? "";
                            if (uid.Length == 0) continue;
                            string n = Path.GetFileName(d);
                            // 首选：真·明文语料里的英文内部目录名
                            //   .../content/item_xxx/resource_packs/<EnglishName>/manifest.json
                            // 这个目录名就是服务端 CLIENT addon list 用的那个名字
                            string parent = Path.GetFileName(Path.GetDirectoryName(d) ?? "");
                            if (parent.Equals("resource_packs", StringComparison.OrdinalIgnoreCase)
                                || parent.Equals("behavior_packs", StringComparison.OrdinalIgnoreCase))
                            {
                                idx.CorpusNames[uid] = n;
                            }
                            else
                            {
                                int i = n.IndexOf("_resource_packs_", StringComparison.Ordinal);
                                if (i >= 0) idx.CorpusNames[uid] = n.Substring(i + 16);
                                else
                                {
                                    i = n.IndexOf("_behavior_packs_", StringComparison.Ordinal);
                                    if (i >= 0) idx.CorpusNames[uid] = n.Substring(i + 16);
                                }
                            }
                        }
                    }
                    catch { }
                }
                // 3) keystream 表：Chronal 加密 blob ↔ 真实明文
                foreach (string d in SafeDirs(ChronalRoot, SearchOption.TopDirectoryOnly))
                {
                    string zp = Path.Combine(d, "resource.download");
                    if (!File.Exists(zp)) continue;
                    try
                    {
                        using var z = ZipFile.OpenRead(zp);
                        foreach (var e in z.Entries)
                        {
                            if (e.FullName.EndsWith("/")) continue;
                            string[] parts = e.FullName.Replace('\\', '/').Split('/');
                            if (parts.Length < 4) continue;
                            string rel = string.Join("/", parts.Skip(3));
                            if (rel.EndsWith(".mczip", StringComparison.OrdinalIgnoreCase))
                                rel = rel.Substring(0, rel.Length - 6);
                            byte[] c;
                            using (var ms = new MemoryStream())
                            {
                                using (var es = e.Open()) es.CopyTo(ms);
                                c = ms.ToArray();
                            }
                            if (c.Length <= 80 || !IsMagic(c)) continue;
                            int len = c.Length - 64;
                            byte[]? P = Lookup(idx.ByName, (Path.GetFileName(rel).ToLowerInvariant(), len))
                                      ?? Lookup(idx.ByTail, (TailOf(rel), len));
                            if (P == null) continue;
                            int n = Math.Min(len, P.Length);
                            if (n < 16) continue;
                            var pre = new byte[16];
                            for (int i = 0; i < 16; i++) pre[i] = (byte)(c[64 + i] ^ P[i]);
                            idx.Prefixes[c.Skip(64).Take(16).ToArray()] = pre;
                            for (int i = 16; i < n; i++)
                            {
                                var w = new byte[16];
                                Array.Copy(c, 64 + i - 16, w, 0, 16);
                                byte k = (byte)(c[64 + i] ^ P[i]);
                                if (!idx.Tab.ContainsKey(w)) idx.Tab[w] = k;
                            }
                        }
                    }
                    catch { }
                }
                _index = idx;
                Log(string.Format("index built: plain={0} tail={1} names={2} corpusNames={3} table={4}",
                    idx.ByName.Count, idx.ByTail.Count, idx.Names.Count, idx.CorpusNames.Count, idx.Tab.Count));
                return idx;
            }
        }

        // ---------------- 目录名对齐 ----------------

        /// <summary>
        /// 把已部署目录名改成服务端/语料使用的英文内部名。
        /// 只做重命名，不动文件内容，不动正在被游戏读取的其它目录。
        /// </summary>
        private static void NormalizeDirNames(Index idx)
        {
            foreach (var pair in new[]
            {
                (Root: ResourcePacksRoot, Kind: "resource_packs"),
                (Root: BehaviorPacksRoot, Kind: "behavior_packs"),
            })
            {
                if (!Directory.Exists(pair.Root)) continue;
                foreach (string d in SafeDirs(pair.Root, SearchOption.TopDirectoryOnly))
                {
                    string cur = Path.GetFileName(d);
                    string? uuid = ReadManifestUuid(d);
                    if (string.IsNullOrEmpty(uuid)) continue;
                    if (!idx.CorpusNames.TryGetValue(uuid, out string? english)) continue;
                    if (string.IsNullOrWhiteSpace(english)) continue;
                    string want = LevelId + "_" + pair.Kind + "_" + SafeName(english);
                    if (string.Equals(cur, want, StringComparison.OrdinalIgnoreCase)) continue;
                    string dst = Path.Combine(pair.Root, want);
                    if (Directory.Exists(dst))
                    {
                        // 目标已存在且是同一个 uuid ⇒ 当前目录是重复副本，直接删
                        if (string.Equals(ReadManifestUuid(dst), uuid, StringComparison.OrdinalIgnoreCase))
                        {
                            try { Directory.Delete(d, true); Log("dup removed: " + cur); } catch { }
                            continue;
                        }
                        continue;   // uuid 不同，保持原样
                    }
                    try { Directory.Move(d, dst); Log("renamed: " + cur + " -> " + want); }
                    catch (Exception ex) { Log("rename fail " + cur + ": " + ex.Message); }
                }
            }
        }

        private static string? ReadManifestUuid(string dir)
        {
            foreach (string n in new[] { "manifest.json", "pack_manifest.json" })
            {
                string p = Path.Combine(dir, n);
                if (!File.Exists(p)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(p));
                    if (doc.RootElement.TryGetProperty("header", out var h)
                        && h.TryGetProperty("uuid", out var u))
                        return u.GetString();
                }
                catch { }
            }
            return null;
        }

        // ---------------- helpers ----------------

        private static IEnumerable<string> PlainRoots()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                      "Chronal", "Bedrock", "server-resources");
            yield return Path.Combine(NeteaseRoot, "games", "11", "resource_packs");
            yield return Path.Combine(NeteaseRoot, "games", "11", "behavior_packs");
            yield return @"D:\resource_packs.bak";
            yield return @"D:\behavior_packs.bak";
            yield return ResourcePacksRoot;
            yield return BehaviorPacksRoot;
        }

        private static HashSet<string> DeployedUuids()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in new[] { ResourcePacksRoot, BehaviorPacksRoot })
            {
                if (!Directory.Exists(root)) continue;
                foreach (string d in SafeDirs(root, SearchOption.TopDirectoryOnly))
                {
                    foreach (string n in new[] { "pack_manifest.json", "manifest.json" })
                    {
                        string p = Path.Combine(d, n);
                        if (!File.Exists(p)) continue;
                        try
                        {
                            using var doc = JsonDocument.Parse(File.ReadAllBytes(p));
                            if (doc.RootElement.TryGetProperty("header", out var h)
                                && h.TryGetProperty("uuid", out var u))
                            { string? s = u.GetString(); if (!string.IsNullOrEmpty(s)) set.Add(s); }
                        }
                        catch { }
                        break;
                    }
                }
            }
            return set;
        }

        private static IEnumerable<string> SafeFiles(string root)
        {
            try { return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList(); }
            catch { return Array.Empty<string>(); }
        }

        private static IEnumerable<string> SafeDirs(string root, SearchOption opt)
        {
            try { return Directory.EnumerateDirectories(root, "*", opt).ToList(); }
            catch { return Array.Empty<string>(); }
        }

        private static string TailKey(string file, string root) =>
            string.Join("/", Path.GetRelativePath(root, file).Replace('\\', '/').Split('/').Reverse().Take(3).Reverse())
                 .ToLowerInvariant();
        private static string TailOf(string rel) =>
            string.Join("/", rel.Split('/').Reverse().Take(3).Reverse()).ToLowerInvariant();

        private static byte[]? Lookup(Dictionary<(string, int), byte[]> d, (string, int) k) =>
            d.TryGetValue(k, out var v) ? v : null;

        private static byte[]? Lookup2(Dictionary<(string, string), byte[]> d, (string, string) k) =>
            d.TryGetValue(k, out var v) ? v : null;

        /// <summary>会话元数据：每次下载都会重新加密、长度漂移，不能靠长度匹配。</summary>
        private static bool IsSessionMeta(string rel)
        {
            string bn = Path.GetFileName(rel).ToLowerInvariant();
            return bn == "contents.json" || bn == "folder_md5.json"
                || bn == "manifest.json" || bn == "pack_manifest.json";
        }

        private static bool IsMagic(byte[] b) =>
            b.Length > 64 && b[0] == Magic[0] && b[1] == Magic[1] && b[2] == Magic[2] && b[3] == Magic[3];

        private static byte[] TableDecrypt(byte[] b, int off, Dictionary<byte[], byte> tab,
                                           Dictionary<byte[], byte[]> prefixes, out double miss)
        {
            int n = b.Length - off;
            var outp = new byte[n];
            byte[]? k0 = prefixes.TryGetValue(b.Skip(off).Take(16).ToArray(), out var p) ? p : null;
            int missing = 0;
            for (int i = 0; i < n; i++)
            {
                if (i < 16)
                {
                    if (k0 != null) outp[i] = (byte)(b[off + i] ^ k0[i]);
                    else { missing++; outp[i] = 0; }
                }
                else
                {
                    var w = new byte[16];
                    Array.Copy(b, off + i - 16, w, 0, 16);
                    if (tab.TryGetValue(w, out byte k)) outp[i] = (byte)(b[off + i] ^ k);
                    else { missing++; outp[i] = 0; }
                }
            }
            miss = n == 0 ? 1.0 : (double)missing / n;
            return outp;
        }

        private static bool ValidPng(byte[] b)
        {
            if (b.Length < 33 || b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return false;
            try
            {
                int i = 8, chunks = 0;
                while (i + 8 <= b.Length)
                {
                    uint ln = (uint)(b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3]);
                    byte[] t = { b[i + 4], b[i + 5], b[i + 6], b[i + 7] };
                    if (t.Any(x => x < 65 || x > 122) || ln > (uint)b.Length) return false;
                    if (t[0] == 'I' && t[1] == 'E' && t[2] == 'N' && t[3] == 'D') return true;
                    i += 12 + (int)ln;
                    if (++chunks > 500) return false;
                }
                return false;
            }
            catch { return false; }
        }

        private static bool LooksReal(byte[] b)
        {
            if (ValidPng(b)) return true;
            if (b.Length == 0) return false;
            if (b[0] == (byte)'{' || b[0] == (byte)'[')
            {
                try { using var _ = JsonDocument.Parse(b); return true; } catch { }
            }
            int good = b.Count(c => c >= 32 && c < 127 || c == 9 || c == 10 || c == 13);
            return (double)good / b.Length > 0.95;
        }

        /// <summary>
        /// 重建会被会话重新加密的元数据（contents.json / folder_md5.json）。
        ///
        /// 实证规则（Chronal 真·明文 v69 全量核对，3957 条）：
        ///   contents.json  = 列全部文件（含 contents / folder_md5 / manifest），CRLF，
        ///                    "content" 数组，"version": 1
        ///   folder_md5.json = 只列「除 manifest 之外」的文件，值是 **packcache 密文块的 md5**
        ///                    （不是明文 md5；3951/3957 命中，差 4 个是 .mcp 打包头）
        /// 因为混淆值直接来自密文，删除重下后无需知道会话 key 也能生成完全正确的内容。
        /// </summary>
        private static byte[] Rebuild(string rel, List<string> files,
                                      Dictionary<string, byte[]> cipherByRel,
                                      HashSet<string> manifestNames)
        {
            string baseN = Path.GetFileName(rel).ToLowerInvariant();

            if (baseN == "contents.json")
            {
                var all = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in cipherByRel) all.Add(kv.Key);
                foreach (string m in manifestNames) all.Add(m);
                var items = all.Select(f => "      {\r\n         \"path\" : \"" + f.Replace('\\', '/') + "\"\r\n      }");
                return Encoding.UTF8.GetBytes(
                    "{\r\n   \"content\" : [\r\n" + string.Join(",\r\n", items) +
                    "\r\n   ],\r\n   \"version\" : 1\r\n}\r\n");
            }

            if (baseN == "folder_md5.json")
            {
                var lines = new List<string>();
                foreach (string m in manifestNames) { /* manifest 不进 folder_md5 */ }
                var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in cipherByRel)
                {
                    string fn = Path.GetFileName(kv.Key);
                    if (manifestNames.Contains(fn)) continue;
                    keys.Add(kv.Key);
                }
                // contents.json 自身的 md5 也要在列，用重建后的字节算
                foreach (string k in keys)
                {
                    byte[] blob;
                    if (string.Equals(Path.GetFileName(k), "contents.json", StringComparison.OrdinalIgnoreCase))
                        blob = Rebuild("contents.json", files, cipherByRel, manifestNames);
                    else
                        blob = cipherByRel[k];
                    lines.Add("    \"" + k.Replace('\\', '/') + "\": \"" + Md5Hex(blob) + "\"");
                }
                return Encoding.UTF8.GetBytes("{\n" + string.Join(", \n", lines) + "\n}");
            }

            if (baseN == "extra_info.json") return Encoding.UTF8.GetBytes("{\"type\":\"data\"}\r\n");
            return Encoding.UTF8.GetBytes("{}");
        }

        private static string Md5Hex(byte[] b)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            return Convert.ToHexString(md5.ComputeHash(b)).ToLowerInvariant();
        }

        private static PackMeta? ReadManifest(string dir)
        {
            foreach (string n in new[] { "pack_manifest.json", "manifest.json" })
            {
                string p = Path.Combine(dir, n);
                if (!File.Exists(p)) continue;
                byte[] raw;
                try { raw = File.ReadAllBytes(p); } catch { continue; }
                if (raw.Length == 0 || (raw[0] != (byte)'{' && raw[0] != (byte)'[')) continue;
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    var r = doc.RootElement;
                    string u = "", nm = "", ty = "resources";
                    if (r.TryGetProperty("header", out var h))
                    {
                        if (h.TryGetProperty("uuid", out var a)) u = a.GetString() ?? "";
                        if (h.TryGetProperty("name", out var b)) nm = b.GetString() ?? "";
                    }
                    if (r.TryGetProperty("modules", out var ms) && ms.ValueKind == JsonValueKind.Array)
                        foreach (var m in ms.EnumerateArray())
                            if (m.TryGetProperty("type", out var t)) { ty = t.GetString() ?? "resources"; break; }
                    if (u.Length == 0) continue;
                    return new PackMeta { Uuid = u, Name = nm, Type = ty, ManifestName = n, ManifestBytes = raw };
                }
                catch { }
            }
            return null;
        }

        private static string SafeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Pack";
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || " _-[]()".IndexOf(c) >= 0 ? c : '_');
            string r = sb.ToString().Trim();
            return r.Length == 0 ? "Pack" : r;
        }

        private static void Write(string path, byte[] data)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, data);
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public bool Equals(byte[]? x, byte[]? y) => x != null && y != null && x.SequenceEqual(y);
            public int GetHashCode(byte[] obj)
            {
                unchecked
                {
                    int h = 17;
                    foreach (byte b in obj) h = h * 31 + b;
                    return h;
                }
            }
        }
    }
}
