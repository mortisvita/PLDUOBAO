using System;
using System.Text.Json.Serialization;

namespace PLDUOBAO.Network.NewVerify
{
    [JsonSerializable(typeof(AccountSystem.Account))]
    internal partial class AccContext : JsonSerializerContext
    {
    }

    // 本地账户数据容器。
    // 原先的远程鉴权（HTTPProxy / AppSecrets / AntiMitm）指向的服务器已停服，
    // 相关逻辑连同登录态管理一并移除，此处仅保留账户数据的序列化载体。
    internal class AccountSystem
    {
        public static Account? user;

        public class Account
        {
            public string? username { get; set; }
            public string? password { get; set; }
            public string? hwid { get; set; }
            public bool isBeta { get; set; } = false;
            public DateTime avaliableTime { get; set; } = DateTime.MinValue;
        }
    }
}
