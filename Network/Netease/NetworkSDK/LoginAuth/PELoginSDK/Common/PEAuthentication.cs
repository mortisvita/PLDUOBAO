using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    [JsonSerializable(typeof(PEAuthentication))]
    internal partial class PEAuthenticationContext : JsonSerializerContext
    {

    }

    internal class PEAuthentication
    {
        public string? engine_version { get; set; }

        public string? extra_param { get; set; } = "extra";

        public string? message { get; set; }

        public string? patch_version { get; set; }

        public string? pay_channel { get; set; } = "dashen_cloudgame";

        public string? sa_data { get; set; }

        public string? sauth_json { get; set; }

        public string? seed { get; set; }

        public string? sign { get; set; }
    }
}
