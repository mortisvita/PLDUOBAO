using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Netease.NetworkSDK.PECommon
{
    internal class NetGameServerAddress
    {
        [JsonPropertyName("host")]
        public required string Host { get; set; }

        [JsonPropertyName("port")]
        public required int Port { get; set; }
    }
}
