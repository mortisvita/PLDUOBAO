using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PLDUOBAO.Util
{
    // 日志脱敏。
    // 登录链路上大量日志会打印渠道返回的原始 JSON，其中包含
    // access_token / sessionid / 身份证 / 实名 / sauth_json / udid 等凭据或个人
    // 信息。DEBUG 控制台会把它们明文显示出来（也容易被截图外泄），所以所有
    // 打印点统一走这里做打码。
    // 只打码「值」，保留字段名和结构，诊断能力不丢失。
    // 同时兼容普通 JSON ("key":"value") 与 System.Text.Json 转义后的
    // \u0022key\u0022:\u0022value\u0022 形式。
    internal static class LogRedactor
    {
        // 需要打码的字段名（不区分大小写）。
        private static readonly string[] SensitiveKeys =
        {
            "access_token", "refresh_token", "token", "gas_token",
            "sessionid", "session_id", "state", "sign", "chl_sdk_json",
            "idcard", "realname", "bindedphone", "full_bind_phone", "phone",
            "password", "pwd", "userpass",
            "sauth_json", "cookie", "set-cookie", "authorization",
            "udid", "first_udid", "deviceid", "device_id", "mac_addr", "tdid",
            "client_login_sn", "sdkuid", "userid", "username", "nick",
        };

        private const string KeyPattern =
            "(?:" + "access_token|refresh_token|token|gas_token|" +
            "sessionid|session_id|state|sign|chl_sdk_json|" +
            "idcard|realname|bindedphone|full_bind_phone|phone|" +
            "password|pwd|userpass|" +
            "sauth_json|cookie|set\\-cookie|authorization|" +
            "udid|first_udid|deviceid|device_id|mac_addr|tdid|" +
            "client_login_sn|sdkuid|userid|username|nick" + ")";

        // 普通 JSON: "key" : "value"
        private static readonly Regex PlainJson = new(
            @"(?i)(""" + KeyPattern + @"""\s*:\s*"")((?:\\.|[^""\\])*)(""?)",
            RegexOptions.Compiled);

        // System.Text.Json 转义形式: \u0022key\u0022:\u0022value\u0022
        private static readonly Regex EscapedJson = new(
            @"(?i)(\\u0022" + KeyPattern + @"\\u0022\s*:\s*\\u0022)([^\\]*?)(\\u0022)",
            RegexOptions.Compiled);

        // 查询串形式: key=value&
        private static readonly Regex QueryParam = new(
            @"(?i)([?&]" + KeyPattern + @"=)([^&\s""<]*)",
            RegexOptions.Compiled);

        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            int n = value.Length;
            if (n <= 4) return "***";
            // 保留少量首尾便于比对是否是同一个值，但不足以还原
            return value[..2] + "…" + $"<{n} chars>" + "…" + value[^2..];
        }

        // 把字符串里的敏感值打码。
        public static string Redact(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string s = PlainJson.Replace(text, m =>
                m.Groups[1].Value + Mask(m.Groups[2].Value) + m.Groups[3].Value);

            s = EscapedJson.Replace(s, m =>
                m.Groups[1].Value + Mask(m.Groups[2].Value) + m.Groups[3].Value);

            s = QueryParam.Replace(s, m => m.Groups[1].Value + Mask(m.Groups[2].Value));

            return s;
        }

        // 安全摘要：只报长度与顶层字段名，完全不输出值。
        // 用于整段凭据 JSON（例如 4399Cookie）。
        public static string Brief(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "<empty>";
            var keys = PlainJson.Matches(text)
                                .Cast<Match>()
                                .Select(m => m.Groups[1].Value.Trim('"', ':', ' ', '\\'))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(12)
                                .ToArray();
            string keyPart = keys.Length > 0 ? string.Join(",", keys) : "no-json-fields";
            return $"<{text.Length} chars, fields: {keyPart}>";
        }
    }
}
