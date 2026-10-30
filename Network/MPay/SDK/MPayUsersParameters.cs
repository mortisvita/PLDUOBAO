using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.MPay.SDK
{
    [JsonSerializable(typeof(MPayUsersParameters))]
    internal partial class MPayUsersParametersContext : JsonSerializerContext { }
    internal class MPayUsersParameters
    {
        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;

        [JsonPropertyName("unique_id")]
        public string Unique { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;
    }
}
