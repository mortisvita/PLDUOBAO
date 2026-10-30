using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK
{
    internal class VerifyException :Exception
    {
        public string verifyUrl { get; set; } = "";
        public VerifyException(string message,string verifyUrl)
        : base(message)
        {
            this.verifyUrl = verifyUrl;
        }
    }
}
