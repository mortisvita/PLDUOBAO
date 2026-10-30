using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    [JsonSerializable(typeof(LoginSercurity))]
    internal partial class LoginSercurityContext : JsonSerializerContext
    {

    }
    internal class LoginSercurity
    {
        [JsonPropertyName("aa_IL1il1ilili1lili1li")]
        public string? PE_url { get; set; }
        [JsonPropertyName("bb_li1li1li1Li1li1i")]
        public string? engineVersion { get; set; }
        [JsonPropertyName("cc_li1li1li111il1i1li1")]
        public string? patchVersion { get; set; }
        [JsonPropertyName("dd_O00o0o0o00O0o0o0Oo0o")]
        public string? libminecraftpe { get; set; }
        [JsonPropertyName("ee_O0o0o0o0o0o0o0O0o0oOo0")]
        public string? patch { get; set; }
        [JsonPropertyName("ff_O0o0o0o0o0o0o0O0o0oOo0")]
        public string? apkSignHash { get; set; }
        [JsonPropertyName("diouawdiuywadhiouwadsgiudioauywgd")]
        public int offset { get; set; }
        [JsonPropertyName("d2oawgdouiwaigduwahdowailduh")]
        public int rounds { get; set; }
    }
}
