using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLDUOBAO.Network.MPay.SDK.Response;
using PLDUOBAO.Network.MPay.SDK;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace PLDUOBAO.Network.Netease.NetworkSDK.Cookie
{
    [JsonSerializable(typeof(X19CookieRequest))]
    internal partial class X19CookieRequestContext : JsonSerializerContext
    {

    }
    internal class X19CookieRequest
    {
        [JsonPropertyName("sauth_json")]
        public required string Json { get; set; }
        public static X19CookieRequest Generate(MPayUserResponse user, MPayDevice device)
        {
            return new X19CookieRequest
            {
                Json = JsonSerializer.Serialize(new X19Cookie 
                {
                    SdkUid = user.User.Id,
                    SessionId = user.User.Token,
                    Udid = Guid.NewGuid().ToString("N").ToUpper(),
                    DeviceId = device.Id
                },X19CookieContext.Default.X19Cookie),
            };
        }
    }
}
