using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Exceptions
{
    internal class LoginFailedException(LoginFailedEntity entity) : Exception()
    {
        public LoginFailedEntity? Entity { get; private set; } = entity;

        public string GetFinalError()
        {
            if (entity.code == 404 && entity.subcode == 0)
                return "该Cookie未激活，请更换Cookie。";

            if (entity.code == 401 && entity.subcode == 18)
                return "该Cookie已过期，请更换Cookie";

            return $"登录失败，错误码：{entity.code}，子错误码：{entity.subcode}，状态：{entity.status}";
        }

        public override string ToString()
        {
            return GetFinalError();
        }
    }
}
