namespace PLDUOBAO.Network.Netease.NetworkSDK.LoginAuth
{
    // PE 鉴权的客户端指纹。
    // 【重要】必须使用与登录渠道一致的渠道包。
    // 不同渠道的 libminecraftpe.so 不是同一个文件，混用会被判定为
    // code 2001「客户端版本过低」。实测差异：
    // netease.qqchannel_free : so 323,630,848 字节 / md5 393dfdf6...
    // netease（4399com 渠道）: so 311,740,544 字节 / md5 6f0fefb1...  ← 本文件采用
    // 全部取自 4399 官方渠道包：
    // https://sj.img4399.com/game_list/294/com.netease.mc.m4399/g.4hhmh43i.v8304182.apk
    // （1,879,575,153 字节，versionCode 8304182，versionName 3.10.5.301347）
    // 各字段来源：
    // EngineVersion        libminecraftpe.so 内嵌版本串（全 so 唯一一处）
    // LibMinecraftpeSoHash md5(lib/arm64-v8a/libminecraftpe.so)
    // PatchVersion         客户端运行时 app_ver，登录界面左上角显示的值
    // PatchHash            补丁包 md5
    // ApkSignHash          md5(签名证书 DER)，885 字节那张；跨渠道恒定
    // SignIndex/SignLoopTimes  PE 签名查表 offset 与轮数
    // PE 请求 message 拼接顺序：
    // EngineVersion + LibMinecraftpeSoHash + PatchVersion + PatchHash + ApkSignHash + guid
    // 验证状态（2026-11-06）：
    // 使用本组值 → 服务端返回 code 32（版本校验通过，进入 sauth 校验阶段）
    // 使用 qqchannel 的 so hash → code 2001（版本过低）
    internal static class VerificationProfile
    {
        // PE 签名取 signShiftTable / signConstantTable 的起始下标。
        public const int SignIndex = 2;

        // PE 签名混合轮数。
        public const int SignLoopTimes = 8;

        // md5(签名证书 DER)，跨渠道恒定（qqchannel 与 4399 包实测一致）。
        public const string ApkSignHash = "2b3e7ca013bb30a74d822579860c042b";

        // 4399 渠道包 libminecraftpe.so 内嵌版本号。
        public const string EngineVersion = "3.10.5.301347";

        // md5(4399 渠道的 lib/arm64-v8a/libminecraftpe.so)，311,740,544 字节。
        public const string LibMinecraftpeSoHash = "6f0fefb1833f16f825c6248c290c0293";

        // 热更补丁版本 = 客户端运行时上报的 app_ver。
        public const string PatchVersion = "3.10.12.301658";

        // 与 PatchVersion 配对的补丁包 md5。
        public const string PatchHash = "833f5404381fb9011189467a1a4e50d2";

        // 本组取值的采集时间（Unix 秒，2026-11-06）。
        public const long UpdatedAt = 1793980800;
    }
}
