using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth.PELoginSDK.Common
{
    internal class LoginFailedEntity
    {
        public int code { get; set; }
        public int subcode { get; set; }
        public string? status { get; set; }
    }
}
