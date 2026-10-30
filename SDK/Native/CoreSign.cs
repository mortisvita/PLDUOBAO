using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Codexus.Server.G79;

namespace PLDUOBAO.SDK.Native
{
    internal class CoreSign
    {
        public unsafe static byte[]? PESignCount(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }
            return PESign.PESignCount(message);
        }

        public unsafe static byte[]? PESignCount(string message, int offset, int rounds)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }
            return PESign.PESignCount(message, offset, rounds);
        }
    }
}
