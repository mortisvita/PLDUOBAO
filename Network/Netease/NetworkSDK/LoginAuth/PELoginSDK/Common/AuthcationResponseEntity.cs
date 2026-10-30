using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    [JsonSerializable(typeof(AuthcationResponseEntity))]
    internal partial class AuthcationResponseEntityContext : JsonSerializerContext
    {

    }
    internal class AuthcationResponseEntity
    {
        public string? entity_id { get; set; }
        public string? account { get; set; }
        public string? token { get; set; }
        public string? sead { get; set; }
        public bool hasMessage { get; set; }
        public long? aid { get; set; }
        public string? sdkuid { get; set; }
        public string? access_token { get; set; }
        public string? unisdk_login_json { get; set; }
        public int verify_status { get; set; }
        public bool hasGmail { get; set; }
        public bool is_register { get; set; }
        public List<string>? autopatch { get; set; }
        public string? env { get; set; }
        public int last_server_up_time { get; set; }
        public string? min_engine_version { get; set; }
        public string? min_patch_version { get; set; }
    }
}
