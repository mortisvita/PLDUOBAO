using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PLDUOBAO.Util
{
    public enum RenderEngine
    {
        OpenGL = 0,
        RenderDragon = 1
    }

    public enum GameEdition
    {
        NeteasePE = 0,
        NeteasePB = 1
    }

    internal static class RenderEngineHelper
    {
        private const string Key = "gfx_renderengine:";

        public static string GetOptionsTxtPath(GameEdition edition)
        {
            string folder = edition == GameEdition.NeteasePE
                ? "MinecraftPE_Netease"
                : "MinecraftPC_Netease_PB";

            string sub = edition == GameEdition.NeteasePE ? "minecraftpe" : "minecraftp";

            string primary = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                folder, sub, "options.txt");

            if (File.Exists(primary))
                return primary;

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "Roaming", folder, sub, "options.txt");
        }

        public static bool TryReadRenderEngine(GameEdition edition, out RenderEngine engine)
        {
            engine = RenderEngine.OpenGL;

            string path = GetOptionsTxtPath(edition);
            if (!File.Exists(path))
                return false;

            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    if (!raw.StartsWith(Key, StringComparison.Ordinal))
                        continue;

                    string val = raw.Substring(Key.Length).Trim();
                    if (int.TryParse(val, out int n))
                    {
                        engine = n == 1 ? RenderEngine.RenderDragon : RenderEngine.OpenGL;
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
            }
            return false;
        }

        public static bool WriteRenderEngine(GameEdition edition, RenderEngine engine)
        {
            string path = GetOptionsTxtPath(edition);
            if (!File.Exists(path))
                return false;

            string[] original;
            try
            {
                original = File.ReadAllLines(path);
            }
            catch
            {
                return false;
            }

            var output = NormalizeLines(original, engine);

            string tmp = path + ".tmp";
            try
            {
                File.WriteAllText(tmp, string.Join("\r\n", output), Encoding.UTF8);
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
                return true;
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return false;
            }
        }

        private static List<string> NormalizeLines(string[] original, RenderEngine engine)
        {
            var result = new List<string>(original.Length + 1);
            bool updated = false;

            foreach (var raw in original)
            {
                int idx = raw.IndexOf(Key, StringComparison.Ordinal);
                if (idx < 0)
                {
                    result.Add(raw);
                    continue;
                }

                if (idx == 0)
                {
                    result.Add($"{Key}{(int)engine}");
                    updated = true;
                    continue;
                }

                string head = raw.Substring(0, idx);
                if (!string.IsNullOrWhiteSpace(head))
                    result.Add(head);

                result.Add($"{Key}{(int)engine}");
                updated = true;
            }

            if (!updated)
                result.Add($"{Key}{(int)engine}");

            return result;
        }
    }
}