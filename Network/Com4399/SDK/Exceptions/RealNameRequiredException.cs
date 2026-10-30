using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK.Exceptions
{
    internal class RealNameRequiredException :Exception
    {
        public string verifyUrl { get; set; } = "";
        public RealNameRequiredException(string message, string verifyUrl)
        : base(message)
        {
            this.verifyUrl = verifyUrl;
        }
    }
}
