using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;
using PLDUOBAO.Network.Netease.NetworkSDK.PCCommon;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon;
using PLDUOBAO.Network.Netease.NetworkSDK.PECommon.Requests;
using static PLDUOBAO.Network.Netease.NetworkLocalAPI;

namespace PLDUOBAO.Network.Netease
{
    internal class NeteasePosts
    {
        //对于content为空的内容，直接为""即可。
        //如果你要新增请求，一定要记得去ApiResponse<T>去加。

        public static async Task<ApiResponse<UserDetail>?> GetUserDetailAsync()
        {
            return await G79PostAsync(PostMode.Core,
                                      "", 
                                      "/pe-user-detail/get",
                                      ApiResponseContext.Default.ApiResponseUserDetail);
        }

        public static async Task<ApiResponse<NetGame>?> GetAvailableNetGamesAsync()
        {
            var body = JsonSerializer.Serialize(
                new EntityNetGameRequest
                { 
                    ChannelId = 5,
                    Version = "2.12"
                }
                , EntityNetGameRequestContext.Default.EntityNetGameRequest);

            return await G79PostAsync(PostMode.Client,
                                      body,
                                      "/pe-game/query/get-list-v4",
                                      ApiResponseContext.Default.ApiResponseNetGame);
        }

        public static async Task<ApiResponse<NetGameServerAddress>?> GetNetGameServerAddress(string ItemId)
        {
            var body = JsonSerializer.Serialize(
                new EntityNetGameServerAddressRequest
                {
                   ItemId = ItemId,
                }
                , EntityNetGameServerAddressRequestContext.Default.EntityNetGameServerAddressRequest);

            return await G79PostAsync(PostMode.Client,
                                      body,
                                      "/pe-game/query/get-server-address",
                                      ApiResponseContext.Default.ApiResponseNetGameServerAddress);
        }

        public static async Task<ApiResponse<RentalGameServerAddress>?> GetRentalGameServerAddress(string ServerId,string Password)
        {
            var body = JsonSerializer.Serialize(
                new EntityRentalGameServerAddressRequest
                {
                    ServerId = ServerId,
                    Password = Password
                }
                , EntityRentalGameServerAddressRequestContext.Default.EntityRentalGameServerAddressRequest);

            return await G79PostAsync(PostMode.Client,
                                      body,
                                      "/rental-server-world-enter/get",
                                      ApiResponseContext.Default.ApiResponseRentalGameServerAddress);
        }

        public static async Task<ApiResponse<EntitySetNickNameRequest>?> SetNickNameAsync(string Name)
        {
            var body = JsonSerializer.Serialize(
                new EntitySetNickNameRequest
                {
                    Name = Name,
                    EntityId = CurrentAvaliableUser?.entity_id!
                }
                , EntitySetNickNameRequestContext.Default.EntitySetNickNameRequest);

            return await G79PostAsync(PostMode.Client,
                                      body,
                                      "/nickname-setting",
                                      ApiResponseContext.Default.ApiResponseEntitySetNickNameRequest);
        }

        public static async Task<ApiResponse<EntityPcNickNameUpdateEntity>?> UpdatePcNickNameAsync(string name)
        {
            var body = JsonSerializer.Serialize(
                new EntityPcNickNameUpdateRequest { Name = name },
                EntityPcNickNameUpdateRequestContext.Default.EntityPcNickNameUpdateRequest);

            return await GatewayPostAsync(
                "/pc-nickname-setting/update",
                body,
                ApiResponseContext.Default.ApiResponseEntityPcNickNameUpdateEntity);
        }

        public static async Task<ApiResponse<EntityGatewayUser>?> SearchUserByUidAsync(string? userId = null)
        {
            userId ??= CurrentAvaliableUser?.entity_id;
            if (string.IsNullOrEmpty(userId))
                return null;

            var body = JsonSerializer.Serialize(
                new EntityUserSearchByUidRequest { UserId = userId },
                EntityUserSearchByUidRequestContext.Default.EntityUserSearchByUidRequest);

            return await GatewayPostAsync(
                "/user/query/search-by-uid",
                body,
                ApiResponseContext.Default.ApiResponseEntityGatewayUser);
        }

        public static async Task RefreshCurrentUserProfileAsync()
        {
            string? name = null;

            var search = await SearchUserByUidAsync();
            if (search?.code == 0 && !string.IsNullOrEmpty(search.entity?.Name))
                name = search.entity.Name;

            var detail = await GetUserDetailAsync();
            if (detail?.code == 0 && detail.entity != null)
            {
                CurrentLevel = detail.entity.level;
                if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(detail.entity.name))
                    name = detail.entity.name;
            }

            if (!string.IsNullOrEmpty(name))
                CurrentUserName = name;
        }

        public static async Task<RentalGame?> GetAvaliableRentalGames(int offset)
        {
            var body = JsonSerializer.Serialize(
                new EntityRentalGameRequest 
                { 
                    Offset = offset,
                    SortType = 0,
                    OrderType = 0,
                
                },EntityRentalGameRequestContext.Default.EntityRentalGameRequest);

            return await G79PostAsyncRegular(PostMode.Client,
                                      body,
                                      "/rental-server/query/available-by-sort-type",
                                      RentalGameContext.Default.RentalGame);
        }

        public static async Task<RentalGame?> GetRentalGamesByServerId(string ServerId)
        {
            var body = JsonSerializer.Serialize(
                new EntityRentalGameSearchServerIdRequest
                {
                    Offset = 0,
                    ServerId = ServerId,

                }, EntityRentalGameSearchServerIdRequestContext.Default.EntityRentalGameSearchServerIdRequest);

            return await G79PostAsyncRegular(PostMode.Client,
                                      body,
                                      "/rental-server/query/search-by-name",
                                      RentalGameContext.Default.RentalGame);
        }

        public static async Task<string?> AuthcationG79V2Async(AuthcationG79 auth)
        {

            var options = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            var context = new AuthcationG79Context(options);

            string body = JsonSerializer.Serialize(auth, context.AuthcationG79);

            LogSys.Log($"[AuthServer] Request Body: {body}");

            var result = await HttpPost_v2("/authentication-v2", body, "https://g79authobt.minecraft.cn");

            return result;

            //string array = "";
            //HttpPost_v2("/authentication-v2",body, "https://g79authobt.minecraft.cn",out array);
            //return array;

            //return await HttpPost_v2Async("/authentication-v2",
            //                              body,
            //                              "https://g79authobt.minecraft.cn");

            //return await G79PostAsync(PostMode.Auth,
            //                          body,
            //                          "/authentication-v2");
        }

        public static async Task<ApiResponse<AuthenticationUpdate>?> AuthenticationUpdateAsync()
        {
            if (CurrentAvaliableUser == null)
            {
                LogSys.Err("不是。没登录怎么能走到这里来的?");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.entity_id))
            {
                LogSys.Err("UID为空。");
                return null;
            }

            if (string.IsNullOrEmpty(CurrentAvaliableUser.token))
            {
                LogSys.Err("Token为空。");
                return null;
            }

            string entity = JsonSerializer.Serialize(new AuthenticationUpdate 
            {
                EntityId = CurrentAvaliableUser.entity_id,
                IsRegister = true,
            },AuthenticationUpdateContext.Default.AuthenticationUpdate);

            return await X19PostAsync(PostMode.UpdateAuth,
                                      entity,
                                      "/authentication/update",
                                      ApiResponseContext.Default.ApiResponseAuthenticationUpdate);

        }
    }
}
