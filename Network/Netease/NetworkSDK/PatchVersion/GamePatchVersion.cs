using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PatchVersion
{
    [JsonSerializable(typeof(GamePatchVersion))]
    [JsonSerializable(typeof(Dictionary<string, GamePatchVersion>))]
    internal partial class GamePatchVersionContext : JsonSerializerContext
    {

    }

    internal class GamePatchVersion
    {
        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";

        [JsonPropertyName("md5")]
        public string Md5 { get; set; } = "";
    }
}
