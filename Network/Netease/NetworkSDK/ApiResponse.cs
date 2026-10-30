using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.PCCommon;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    [JsonSerializable(typeof(ApiResponse<AuthcationResponseEntity>))]
    [JsonSerializable(typeof(ApiResponse<UserDetail>))]
    [JsonSerializable(typeof(ApiResponse<NetGame>))]
    [JsonSerializable(typeof(ApiResponse<EntitySetNickNameRequest>))]
    [JsonSerializable(typeof(ApiResponse<EntityPcNickNameUpdateEntity>))]
    [JsonSerializable(typeof(ApiResponse<EntityGatewayUser>))]
    [JsonSerializable(typeof(ApiResponse<NetGameServerAddress>))]
    [JsonSerializable(typeof(ApiResponse<AuthenticationUpdate>))]
    [JsonSerializable(typeof(ApiResponse<RentalGameServerAddress>))]
    [JsonSerializable(typeof(ApiResponse<LoginFailedEntity>))]
    internal partial class ApiResponseContext : JsonSerializerContext
    {

    }
    internal class ApiResponse<T>
    {
        public int code { get; set; }
        public string? message { get; set; }
        public string? details { get; set; }
        public T? entity { get; set; }
    }
}
