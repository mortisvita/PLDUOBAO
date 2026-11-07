using System.Text.Json.Serialization;

namespace PLDUOBAO.Network.MPay.SDK.Response
{
    // ────────────────────────────────────────────────
    //  创建二维码响应
    // ────────────────────────────────────────────────
    [JsonSerializable(typeof(MPayQRCreateResponse))]
    internal partial class MPayQRCreateResponseContext : JsonSerializerContext { }

    internal class MPayQRCreateResponse
    {
        [JsonPropertyName("uuid")]
        public string Uuid { get; set; } = string.Empty;

        // 出错时有 code / reason
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    // ────────────────────────────────────────────────
    //  轮询二维码状态响应
    // ────────────────────────────────────────────────
    [JsonSerializable(typeof(MPayQRQueryResponse))]
    [JsonSerializable(typeof(MPayQRCodeInfo))]
    [JsonSerializable(typeof(MPayQRLoginInfo))]
    internal partial class MPayQRQueryResponseContext : JsonSerializerContext { }

    internal class MPayQRQueryResponse
    {
        // 出错时有 code / reason
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("qrcode")]
        public MPayQRCodeInfo? QrCode { get; set; }

        [JsonPropertyName("login_info")]
        public MPayQRLoginInfo? LoginInfo { get; set; }
    }

    internal class MPayQRCodeInfo
    {
        // 0=未扫  1=已扫待确认  2=已确认
        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    internal class MPayQRLoginInfo
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("login_channel")]
        public string LoginChannel { get; set; } = "netease";
    }

    // ────────────────────────────────────────────────
    //  loadingbaycn ticket 响应（网易渠道二步换票）
    // ────────────────────────────────────────────────
    [JsonSerializable(typeof(MPayTicketResponse))]
    [JsonSerializable(typeof(MPayTicketData))]
    internal partial class MPayTicketResponseContext : JsonSerializerContext { }

    internal class MPayTicketResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("data")]
        public MPayTicketData? Data { get; set; }
    }

    internal class MPayTicketData
    {
        [JsonPropertyName("ticket")]
        public string Ticket { get; set; } = string.Empty;
    }

    // ────────────────────────────────────────────────
    //  ticket 换 token 响应（login/ticket 接口）
    // ────────────────────────────────────────────────
    [JsonSerializable(typeof(MPayTicketLoginResponse))]
    internal partial class MPayTicketLoginResponseContext : JsonSerializerContext { }

    internal class MPayTicketLoginResponse
    {
        [JsonPropertyName("code")]
        public int? Code { get; set; }

        [JsonPropertyName("reason")]
        public string? Reason { get; set; }

        [JsonPropertyName("user")]
        public MPayUser? User { get; set; }
    }
}
