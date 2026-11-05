# PLDUOBAO

> **本项目代码由豆包（Doubao）编写。**

一个面向 Minecraft 基岩版（Bedrock Edition）的启动器，使用 Avalonia 构建，界面遵循 WinUI 3 / Fluent Design 设计语言。

用于登录 4399 渠道账号、浏览网络服务器列表、管理多账户，并启动本地安装的 Minecraft 基岩版游戏。**内置网易服务器资源包的自动下载、解密与部署管线**，进服前把服务器自定义材质 / 模型 / UI 准备好，消除方块紫黑格子和自定义模型不显示的问题。

![界面风格](https://img.shields.io/badge/UI-Fluent%20Design-2ea44f) ![框架](https://img.shields.io/badge/Avalonia-11.3.11-8A2BE2) ![运行时](https://img.shields.io/badge/.NET-9.0-512BD4) ![平台](https://img.shields.io/badge/Windows-x64-0078D4)

---

## 目录

- [免责声明](#免责声明)
- [功能特性](#功能特性)
- [界面预览](#界面预览)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [快速开始](#快速开始)
- [构建指南](#构建指南)
- [配置说明](#配置说明)
- [服务器资源包管线](#服务器资源包管线)
- [常见问题](#常见问题)
- [工作原理](#工作原理)
- [开发文档](#开发文档)
- [许可与致谢](#许可与致谢)

---

## 免责声明

本项目**仅供学习与技术研究使用**，不得用于任何商业用途。

- 使用本软件所产生的任何后果由使用者自行承担。
- 本项目与 Mojang Studios、Microsoft、网易、4399 均无任何隶属关系。
- 请支持正版游戏。若您喜欢 Minecraft，请通过官方渠道购买。

---

## 功能特性

### 账号登录

- **4399 渠道登录**：输入 4399 账号密码即可登录，程序自动完成 OAuth 授权、SAuth 换取、网易服务器鉴权的完整链路。
- **密码加密传输**：登录密码经 AES-256-CBC 加密后提交，不在网络请求或日志中暴露明文。
- **会话建立**：登录前自动访问授权页建立会话 Cookie，规避服务端风控拦截。
- **图形验证码**：遇到滑块或点选验证码时弹出本地验证页面，完成交互后自动继续登录流程。
- **代理隔离**：关键 HTTP 客户端显式禁用系统代理，避免本地调试工具残留设置导致连接失败。

### 网络游戏

- 拉取可用服务器列表，展示服务器名称、图标、在线人数、延迟等状态。
- 支持关键词搜索过滤。
- 记录服务器图标缓存，避免重复下载。

### 服务器资源包（核心）

- **自动预下载**：进服前扫描 `packcache`，把服务器下发的资源包 / 行为包全部落地。
- **会话元数据重建**：`contents.json`、`folder_md5.json`、清单文件每次下载都会重新加密且长度漂移，部署器按 `(uuid, 包内相对路径)` 从真·明文语料精确取回。
- **目录名对齐**：部署目录使用服务端 `CLIENT addon list` 要求的英文内部名（如 `FashionAnimationAddon_ResourcePack` 而非界面显示名），名字不符会让游戏回退到加密副本。
- **单清单原则**：每个包只写一个清单文件，避免同一 uuid 被载入两次。
- **运行中增量补齐**：游戏启动后持续监控 `packcache`，对服务器晚下载的脚本包再补一轮，绝不删除游戏正在读取的目录。
- **`.mcp` 脚本容器直落**：MCPK 明文容器原样复制，不做解密。

### 账户管理

- 多账户保存与快速切换。
- 支持为账户添加自定义备注名。
- 支持账户信息导出为文件。
- 账户数据本地加密存储，不上传。

### 游戏启动

- 自定义游戏安装路径。
- 可选显示游戏控制台窗口（便于调试）。
- 设备信息伪装（绕过服务端的设备检测）。
- 记录游戏时长统计（总时长 / 今日时长 / 启动次数）。
- Python 兼容层注入与 `.pyc` 头部修复。

### 外观与个性化

- 主题模式：浅色 / 深色 / 跟随系统。
- 强调色：跟随系统或自定义取色。
- 皮肤预览：支持 2D / 3D 预览、纤细皮肤（Slim）切换、皮肤历史记录。
- 自定义 Skia 着色器视觉效果（按钮光晕、文字描边、页面转场）。

### 其他

- 游戏下载与版本管理。
- 渲染引擎切换（OpenGL / RenderDragon）。
- 缓存清理。

---

## 界面预览

主界面采用左侧导航 + 右侧内容区的经典 WinUI 布局：

| 页面 | 说明 |
|---|---|
| **主页** | 欢迎信息、游戏时长统计卡片 |
| **网络游戏** | 服务器列表与搜索 |
| **设置** | 外观、游戏、渲染引擎等配置项 |

导航栏底部包含账户信息入口（支持修改昵称、退出登录）与 Discord 状态显示。

---

## 技术栈

### 核心框架

| 组件 | 版本 | 用途 |
|---|---|---|
| .NET | 9.0 | 运行时 |
| Avalonia | 11.3.11 | 跨平台 UI 框架 |
| FluentAvaloniaUI | 2.3.0 | WinUI 3 / Fluent Design 控件库 |
| Avalonia.Diagnostics | 11.3.11 | 调试工具（仅 Debug） |

### 渲染与图形

| 组件 | 用途 |
|---|---|
| 2ndLAB.ANGLE.Static | 静态链接的 ANGLE 渲染后端 |
| 2ndLAB.SkiaSharp.Static | 静态链接的 Skia 绘图引擎 |
| SkSL 着色器 | 自定义视觉效果（光晕、描边、Voronoi） |

### 加密与安全

| 组件 | 用途 |
|---|---|
| Portable.BouncyCastle | RSA / AES / ChaCha20 等算法 |
| System.Security.Cryptography.ProtectedData | Windows DPAPI，用于本地凭据保护 |

### 其他

| 组件 | 用途 |
|---|---|
| VC-LTL | 减小 Visual C++ 运行时体积 |
| System.Resources.Extensions | 增强资源序列化支持 |

### 目标平台

- **操作系统**：Windows 10 1809 及以上 / Windows 11
- **架构**：x64
- **权限**：需要管理员权限（设备伪装与游戏注入需要）

---

## 项目结构

```
PLDUOBAO/
├── Assets/                     静态资源（图标等）
├── docs/                       开发文档（需求 / 架构 / 规范 / 门禁 / ADR）
├── scripts/                    构建、检查、发布、Git 初始化脚本
├── Lib/
│   └── PESign.cs               PE 签名算法实现
├── Native/
│   ├── PLDUOBAO.DeviceSpoof64.exe   设备信息伪装组件
│   └── PyCodeExecuter64.dll         本地 Python 执行器
├── Network/
│   ├── Com4399/
│   │   ├── Captcha/            滑块 / 点选验证码处理
│   │   ├── SDK/                4399 SDK 设备与 OAuth 模型
│   │   └── X4399Service.cs     4399 登录与授权主流程
│   ├── MPay/                   网易支付 / 扫码登录
│   ├── Mgb/                    MGB SDK 会话
│   ├── Netease/
│   │   ├── NetworkSDK/         网易 PMPH 协议实现
│   │   │   ├── LoginAuth/      PEAuth 认证、G79 协议、签名
│   │   │   ├── PECommon/       服务器 / 租赁服数据模型
│   │   │   ├── Cookie/         X19 Cookie 生成
│   │   │   ├── Mcp/            服务器资源包管线（见下）
│   │   │   │   ├── McpResourceSync.cs    解密 / 部署主流程
│   │   │   │   ├── McpPackage.cs         MCPK 容器解析
│   │   │   │   ├── RotorDecryptor.cs     旋转加密模块解密
│   │   │   │   └── PycRepairer.cs        .pyc 头部修复
│   │   │   ├── ServerPackScanner.cs      世界 ID 解析 / 包状态扫描
│   │   │   └── CPPLaunchConfig.cs        游戏启动配置生成
│   │   ├── NeteaseGets.cs      版本信息查询
│   │   └── NeteasePosts.cs     服务器列表查询
│   ├── NewVerify/              本地账户加密存储
│   │   ├── AccountSystem.cs    账户数据模型
│   │   ├── Encrypto.cs         AES 加解密与 HWID
│   │   └── Netease_XXTEA.cs    XXTEA 算法
│   ├── HTTP/                   参数构造工具
│   └── LocalAuthServer/        本地鉴权服务（Python 注入）
├── SDK/
│   ├── LocalAccountManager.cs  账户列表管理
│   ├── UserConfig.cs           配置模型
│   └── UserCookie.cs           账户 Cookie 模型
├── Services/
│   └── PEGameDownloadService.cs   游戏下载服务
├── Styles/
│   └── GlobalStyles.axaml      全局样式定义
├── UI/
│   ├── Components/             界面组件
│   │   ├── UCMainMenu           主菜单（导航 + 各页面）
│   │   ├── UCCookieSelector     账户选择与登录
│   │   ├── UCCheckName          角色名确认
│   │   ├── UCJoinGame           加入游戏流程（触发资源包部署）
│   │   ├── UCToast              轻提示
│   │   ├── SelectGame/          服务器列表项
│   │   └── SelectAccount/       账户列表项
│   ├── Effects/                Skia 自定义效果
│   │   ├── SkiaEffect           着色器封装
│   │   ├── SkiaShaderRenderer   渲染器
│   │   ├── ButtonHalo           按钮光晕
│   │   ├── LineTextBlock        文字描边
│   │   └── NavigationFrame      页面转场
│   └── UCFirstLaunchTheme.axaml   首次启动主题选择
└── Util/
    ├── PathUtils.cs            路径与配置管理
    ├── GameLauncher.cs         游戏启动
    ├── GamePlayTracker.cs      时长统计
    ├── SkinPreviewer.cs        皮肤渲染
    ├── RenderEngineHelper.cs   渲染引擎切换
    ├── DiscordRPCManager.cs    Discord 状态同步
    ├── LogSys.cs               日志系统
    └── ...                     其他工具类
```

---

## 快速开始

### 使用发布版

1. 从 Releases 下载最新版本压缩包。
2. **解压到不含中文的路径**（程序会检测，含中文会拒绝启动）。
3. 右键以管理员身份运行 `PLDUOBAO.exe`。
4. 首次启动选择主题，进入登录界面输入 4399 账号密码。

> ⚠️ 程序必须放在**纯英文路径**下，例如 `D:\Games\PLDUOBAO\`，不能是 `D:\游戏\启动器\`。

### 首次配置

1. **设置 → 游戏路径**：选择 Minecraft 基岩版安装目录。
2. **设置 → 皮肤设置**：可选，指定皮肤文件路径。
3. **设置 → 渲染引擎**：根据设备选择 OpenGL 或 RenderDragon。

---

## 构建指南

### 环境要求

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10 1809 或更高版本
- Visual Studio 2022（可选，提供 XAML 设计器支持）

### 调试构建

```bash
git clone https://github.com/mortisvita/PLDUOBAO.git
cd PLDUOBAO
dotnet build PLDUOBAO.csproj -c Debug
```

产物位于 `bin/Debug/net9.0/`。

> 项目声明了 `<Platforms>x64</Platforms>`，部分 IDE 场景下产物落在 `bin/x64/Debug/net9.0/`。两个目录内容一致，把新生成的 `dll/exe/pdb` 拷到实际运行的目录即可。

### 发布构建

Release 配置启用了 NativeAOT 与单文件发布：

```bash
dotnet publish PLDUOBAO.csproj -c Release
```

相关配置：

```xml
<PublishAot>true</PublishAot>
<TrimMode>full</TrimMode>
<RunAOTCompilation>true</RunAOTCompilation>
<OptimizationPreference>Size</OptimizationPreference>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
```

产物位于 `bin/Release/net9.0/win-x64/publish/`。

> 💡 AOT 编译耗时较长（通常 3-8 分钟），且会禁用部分反射功能。
> 若遇到运行时反射相关异常，可临时关闭 `PublishAot` 排查。

### 辅助脚本

| 脚本 | 作用 |
|---|---|
| `scripts/build.ps1` | 一键构建 |
| `scripts/check.ps1` | 质量门禁检查 |
| `scripts/release.ps1` | 发布打包 |
| `scripts/setup-git.ps1` | Git 钩子与本地配置初始化 |

### 注意事项

- **管理员权限**：`app.manifest` 已声明 `requireAdministrator`，编译后无需手动提权。
- **中文路径**：`CJKPathGuard` 会在启动时检查，路径含 CJK 字符则弹窗退出。
- **原生依赖**：`Native/` 下的两个文件会被复制到输出目录，缺少会导致设备伪装功能失效。

---

## 配置说明

配置文件位于 `%APPDATA%\.pl\`：

| 文件 | 说明 |
|---|---|
| `userconfig.ini` | 主配置（路径、主题、开关项） |
| `accountlist` | 账户列表（JSON，含加密后的 Cookie） |
| `options.ini` | 临时账户信息 |
| `cds/` | 设备标识文件（4399 / 网易） |
| `Native/` | 运行时下载的原生组件缓存 |

### 主要配置项

| 配置项 | 类型 | 说明 |
|---|---|---|
| `theme` | 枚举 | 浅色 / 深色 / 跟随系统 |
| `useCustomAccentColor` | bool | 是否使用自定义强调色 |
| `customAccentColor` | string | 自定义强调色值 |
| `userGamePath` | string | 游戏安装路径 |
| `userSkinPath` | string | 皮肤文件路径 |
| `isSkinSlim` | bool | 纤细皮肤模型 |
| `showGameConsole` | bool | 是否显示游戏控制台 |
| `enableDeviceSpoof` | bool | 是否启用设备伪装 |
| `gameDownloadPath` | string | 游戏下载目录 |

---

## 服务器资源包管线

Minecraft 基岩版进服时，服务器会把自定义资源包下载到：

```
%APPDATA%\MinecraftPE_Netease\packcache\<Base64目录>\
```

**这些文件是加密的**，文件头是魔数 `90 1D 30 01` + 36 字节 ASCII GUID + 24 字节填充，之后是密文。游戏只有在 `resource_packs/` 下找不到同名包时才回退到这份加密副本 —— 表现出来就是**方块紫黑格子、自定义模型不显示、界面加载失败**。

### 加密结构

```
偏移    内容                                  含义
[0:4]   90 1D 30 01                          魔数
[4:40]  a79b9f8b-5455-3ea7-9e31-37567f85b6aa GUID（36 字节 ASCII）
[40:64] 00 00 ... （24 字节）                 填充
[64:]   B4 13 F6 09 41 CC 9A C2 ...         密文（长度 == 明文长度）
```

判定规则：PNG 文件头 `89 50 4E 47` = 已解密；`90 1D 30 01` = 仍是密文。

### 部署流程

```
点「游玩」
    ↓
McpResourceSync.PrepareAsync()          扫描 packcache，落地已有包
    ↓
ServerPackScanner.ResolveWorldId()      解析世界 ID（4638866978460665031）
    ↓
CPPLaunchConfig                        生成含 level_id 的启动配置
    ↓
启动游戏
    ↓
McpResourceSync.WatchAndDeployAsync()   每 2 秒扫 packcache，最多 8 分钟
    ↓                                   捕获服务器晚下载的脚本包
落地目录 games/com.netease/{resource,behavior}_packs/
<levelId>_<kind>_<英文内部名>/
```

### 三类文件的处理方式

| 类型 | 处理 |
|---|---|
| **静态资源**（PNG / JSON / 模型） | 跨会话密文逐字节相同，按 `(uuid, 相对路径)` 从真·明文语料精确取回；退回 `(文件名, 长度)` 索引 |
| **会话元数据**（`contents.json` / `folder_md5.json` / 清单） | 每次下载重新加密、长度漂移，只能靠 `(uuid, 相对路径)` 定位 |
| **`.mcp` 脚本容器** | MCPK 明文（魔数 `4D 43 50 4B`），原样复制 |

### 已踩过的坑

| 问题 | 后果 | 处理 |
|---|---|---|
| 目录名用界面显示名（`PocketFashion Animation Addon`） | 服务端按英文内部名找不到包 → 回退加密副本 → 紫黑方块 | 对齐 `CLIENT addon list` 的英文内部名 |
| 同一包同时写 `manifest.json` + `pack_manifest.json` | 同一 uuid 被载入两次，`mod id ... version mismatch` | 每个包只保留一个清单 |
| 跨包同名同长度文件（`ui/_ui_defs.json`、`effects/*.json`） | 内容串包，UI 引用了不存在的文件 | 优先 `(uuid, 相对路径)` 精确匹配 |
| 部署时机早于服务器补下载 | 3 个脚本包仍指向 packcache → MCP / GUI 异常 | `WatchAndDeployAsync` 运行中增量补齐 |

详细逆向过程见 [MCP资源包逆向技术文档.md](MCP资源包逆向技术文档.md)。

---

## 常见问题

### 启动时提示"当前程序所在路径包含中文"

将整个程序目录移动到纯英文路径下。这是硬性限制，与 Python 注入组件的路径解析有关。

### 登录提示"该Cookie已过期"

4399 渠道的 sessionid 有效期较短（通常 1-3 天）。请在启动器内点击"使用"重新执行账号密码登录，不要手动粘贴旧 Cookie。

### 登录提示"4399 服务端返回风控限制"

4399 判定当前请求异常，常见原因：

- 短时间内登录过于频繁
- 当前网络 IP 被限制
- 账号存在异地登录

建议等待 10-30 分钟重试，或更换网络环境。

### 登录时报 `10061`（连接被拒绝）

程序启动时已把 `HttpClient.DefaultProxy` 置空，并对登录、验证码、下载等关键客户端显式设置 `UseProxy=false`。若仍出现，检查系统代理是否由第三方工具（Fiddler、Clash 等）设置为一个已失效的端口：

```powershell
netsh winhttp show proxy
Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' ProxyEnable,ProxyServer
```

### 进服后仍有方块紫黑格子

1. 确认 `%APPDATA%\MinecraftPE_Netease\mcp_sync.log` 里最后一次部署是 `bad=0 result=OK`。
2. 确认 `games\com.netease\resource_packs\` 下的目录名是英文内部名（如 `..._FashionAnimationAddon_ResourcePack`）。
3. 检查每个包目录下只有一个清单文件，不是 `manifest.json` 和 `pack_manifest.json` 并存。
4. 若服务器更新了资源包，删掉 `resource_packs` / `behavior_packs` 后重新进服，部署器会全量重建。

### 提示缺少 VC++ 运行库

安装 [Microsoft Visual C++ 2015-2022 Redistributable (x86)](https://aka.ms/vs/17/release/vc_redist.x86.exe) 后重启程序。

### 游戏启动失败

1. 确认「设置 → 游戏路径」指向正确的 Minecraft 安装目录。
2. 确认游戏本体已安装且能独立运行。
3. 在「设置 → 游戏」中打开「显示游戏控制台」，查看具体报错。

---

## 工作原理

### 登录链路

```
用户输入 4399 账号密码
        ↓
[AES-256-CBC 加密密码]
        ↓
GET  ptlogin.4399.com/oauth2/authorize.do   ← 建立会话 Cookie
        ↓
POST ptlogin.4399.com/oauth2/loginAndAuthorize.do
        ↓
获取 SAuth（sauth_json，含 sessionid）
        ↓
POST g79*.minecraft.cn/pe-authentication    ← 网易鉴权
   · 请求体经 x19Crypt 加密
   · 附 PE 签名（PESignCount）
        ↓
返回 entity_id + token，登录完成
```

### 本地鉴权注入

启动游戏时，程序会：

1. 在本地启动一个 HTTP 服务（`127.0.0.1`），模拟网易鉴权端点。
2. 注入 Python 兼容层（`PyCodeExecuter64.dll`），将游戏内的 `os.platform` 伪装为 `android`。
3. 通过 `CPPLaunchConfig` 生成启动配置，将 `auth_server_url` 指向本地服务。

这样游戏启动时请求的鉴权会走到本地，由启动器返回已登录的会话。

---

## 开发文档

| 文档 | 内容 |
|---|---|
| [docs/README.md](docs/README.md) | 文档索引 |
| [docs/01-需求与版本管理.md](docs/01-需求与版本管理.md) | 需求来源、版本号规则、变更记录 |
| [docs/02-架构与模块边界.md](docs/02-架构与模块边界.md) | 分层结构、模块职责、依赖方向 |
| [docs/03-编码与提交规范.md](docs/03-编码与提交规范.md) | 命名、格式、提交信息格式 |
| [docs/04-质量门禁与验收.md](docs/04-质量门禁与验收.md) | 构建 / 检查 / 验收标准 |
| [docs/adr/](docs/adr/) | 架构决策记录 |
| [MCP资源包逆向技术文档.md](MCP资源包逆向技术文档.md) | MCPK 容器格式、加密结构、逆向过程 |

---

## 许可与致谢

本项目未采用开源许可证，**保留所有权利**。

### 第三方组件

| 组件 | 许可证 |
|---|---|
| [Avalonia](https://github.com/AvaloniaUI/Avalonia) | MIT |
| [FluentAvalonia](https://github.com/amwx/FluentAvalonia) | MIT |
| [BouncyCastle](https://www.bouncycastle.org/csharp/) | MIT |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT |
| [ANGLE](https://github.com/google/angle) | BSD-3-Clause |

### 声明

本项目代码由 **豆包** 编写。

界面设计参考了 Microsoft Fluent Design System 规范。
