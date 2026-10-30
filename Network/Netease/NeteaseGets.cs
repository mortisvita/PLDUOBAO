using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.PatchVersion;
using static System.Net.WebRequestMethods;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;

namespace PLDUOBAO.Network.Netease
{
    internal class NeteaseGets
    {
        public static async Task<string> GetLatestVersionAsync()
        {
            var versions = await GetPatchVersionsAsync();

            if (versions == null)
                throw new Exception("NeteaseGets::GetLastedVersionAsync Get Patch Versions Failed : Null");

            return versions.Keys.Last();
        }
        public static async Task<Dictionary<string, GamePatchVersion>?> GetPatchVersionsAsync()
        {
            string text = await (await client.GetAsync("https://x19.update.netease.com/pl/x19_java_patchlist")).Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize("{" + text[..text.LastIndexOf(',')] + "}",GamePatchVersionContext.Default.DictionaryStringGamePatchVersion);
        }
    }
}
