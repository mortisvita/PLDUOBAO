using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK
{
    internal class X4399VipInfo
    {
        [JsonPropertyName("level")]
        public int Level { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }
    }
}
