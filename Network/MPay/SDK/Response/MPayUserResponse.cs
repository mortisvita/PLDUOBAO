using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.MPay.SDK.Response
{
    [JsonSerializable(typeof(MPayUserResponse))]
    internal partial class MPayUserResponseContext : JsonSerializerContext { }
    internal class MPayUserResponse
    {
        [JsonPropertyName("force_pwd")]
        public bool ForcePwd { get; set; }

        [JsonPropertyName("verify_status")]
        public MPayVerifyStatus? VerifyStatus { get; set; }

        [JsonPropertyName("user")]
        public required MPayUser User { get; set; } = new MPayUser();
    }
}
