using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.MPay.SDK
{
    internal class MPayVerifyStatus
    {
        [JsonPropertyName("need_sms")]
        public int NeedSms { get; set; }

        [JsonPropertyName("need_email")]
        public int NeedEmail { get; set; }

        [JsonPropertyName("need_passwd")]
        public int NeedPasswd { get; set; }

        [JsonPropertyName("need_real_name")]
        public int NeedRealName { get; set; }
    }
}
