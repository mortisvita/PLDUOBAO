using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PLDUOBAO.Network.Com4399.SDK
{
    internal class X4399OAuthResult
    {
        [JsonPropertyName("login_url")]
        public string LoginUrl { get; set; } = string.Empty;

        [JsonPropertyName("login_url_backup")]
        public string LoginUrlBackup { get; set; } = string.Empty;

        [JsonPropertyName("login_url_phone")]
        public string LoginUrlPhone { get; set; } = string.Empty;

        [JsonPropertyName("login_url_backup_phone")]
        public string LoginUrlBackupPhone { get; set; } = string.Empty;
    }
}
