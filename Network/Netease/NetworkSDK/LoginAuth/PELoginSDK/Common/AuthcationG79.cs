using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.SDK;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    [JsonSerializable(typeof(AuthcationG79))]
    internal partial class AuthcationG79Context : JsonSerializerContext { }
    internal class AuthcationG79
    {
        public string? bit { get; set; }

        public string? clientKey { get; set; }

        public string? displayName { get; set; }

        public string? engineVersion { get; set; }

        public string? netease_sid { get; set; }

        public string? os_name { get; set; }

        public string? patchVersion { get; set; }

        public long uid { get; set; }
    }
}
