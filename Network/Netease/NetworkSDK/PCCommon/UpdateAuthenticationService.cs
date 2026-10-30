using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Elysium.Core.Utils;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PCCommon
{
    internal class UpdateAuthenticationService
    {
        /// <summary>
        /// 当前运行的Token更新用户实体，
        /// <para>如果更新时这个和 <see cref="NetworkLocalAPI"/> 的用户ID不一致，</para>
        /// <para>优先采用 <see cref="NetworkLocalAPI"/> 的用户，自动取消更新token线程。</para>
        /// </summary>
        public AuthcationResponseEntity? CurrentAvaliableUser { get; set; } = null;

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// 创建Token更新服务，用于更新Token(默认 30 Min / Update)
        /// </summary>
        /// <param name="user">要更新的用户</param>
        public UpdateAuthenticationService(AuthcationResponseEntity user)
        {
            CurrentAvaliableUser = user;
            Task.Factory.StartNew(UpdateCheckThread, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// 后台常运行更新Token线程，
        /// <para>该线程会在与 <see cref="NetworkLocalAPI"/> 的用户ID不一致时自动退出。</para>
        /// </summary>
        /// <returns>返回值是没有意义的</returns>
        private async Task UpdateCheckThread()
        {
            if (NetworkLocalAPI.CurrentAvaliableUser == null ||
                this.CurrentAvaliableUser == null)
                return;

            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        long shouldExpiredTick = DateTimeOffset.Now.AddMinutes(-30.0f).ToUnixTimeMilliseconds();
                        var updateUser = NetworkLocalAPI.CurrentAvaliableUser;

                        if (updateUser == null)
                            return;

                        if (string.IsNullOrEmpty(updateUser.entity_id))
                            return;

                        if (string.IsNullOrEmpty(this.CurrentAvaliableUser.entity_id))
                            return;

                        if (updateUser.entity_id != this.CurrentAvaliableUser.entity_id)
                            return;

                        if (NetworkLocalAPI.CurrentUserLoginTime.ToUnixTimeMilliseconds() > shouldExpiredTick)
                        {
                            await Task.Delay(2000, _cancellationTokenSource.Token);
                            continue;
                        }

                        var result = await NeteasePosts.AuthenticationUpdateAsync();

                        if (result == null || result.entity == null || result.entity.Token == null)
                        {
                            LogSys.Err($"UAS > 更新用户ID<{this.CurrentAvaliableUser.entity_id}>的Token失败。");
                            await Task.Delay(2000,_cancellationTokenSource.Token);
                            continue;
                        }

                        NetworkLocalAPI.CurrentUserLoginTime = DateTimeOffset.Now;
                        NetworkLocalAPI.CurrentAvaliableUser.token = result.entity.Token;

                        LogSys.Log($"UAS > Update User Token Success. UserID = {this.CurrentAvaliableUser.entity_id}");

                        await Task.Delay(2000, _cancellationTokenSource.Token);
                    }
                    catch (Exception ex) 
                    {
                        LogSys.Err($"UAS> 错误：{ex}");
                        await Task.Delay(2000);
                    }
                }
            }
            catch (OperationCanceledException) 
            {
                LogSys.Log($"UAS> 已停止Token更新线程运行。");
            }

        }
    }
}
