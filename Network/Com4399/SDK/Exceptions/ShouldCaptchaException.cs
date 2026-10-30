using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK.Exceptions
{
    internal class ShouldCaptchaException : Exception
    {
        public byte[]? ImageBytes { get; set; }

        public ShouldCaptchaException(string message, byte[] img)
        : base(message)
        {
            this.ImageBytes = img;
        }
    }
}
