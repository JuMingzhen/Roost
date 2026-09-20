# 0001：Windows 客户端技术栈

- 状态：提议
- 日期：2026-09-20
- 提出方：Codex
- 确认人：

## 背景

Roost v1 只支持 Windows，但必须同时满足 PRD 第 4、6、8、9、11、13、18、19 节的约束：小安装包、低常驻资源、透明置顶且仅内容区域接收点击、多显示器/多 DPI、逐帧像素清晰、托盘/全局快捷键/全屏检测/单实例/自启、Windows 安全存储，以及将来的 OpenAI 兼容 HTTP 接口。

M0 的目标不是完成产品代码，而是先排除会让核心桌宠形态失败的技术路线。

## 候选方案

### 方案 A（首选）：C# / .NET Framework 4.8 + Windows Forms + Win32 互操作

普通表单和设置页使用 Windows Forms；桌宠窗口使用无边框 Form，并以 Win32 PMv2 DPI awareness、窗口区域（`SetWindowRgn`）和必要的消息处理控制透明区域、命中区域和显示器切换。像素帧按物理像素整数倍用 nearest-neighbor 绘制。

选择 .NET Framework 4.8 而不是自包含 .NET 8/10，是为了直接使用 Windows 10/11 已包含的运行时，避免把约 60 MB 的自包含运行时塞进安装包。Microsoft 当前文档确认 Windows 10 22H2 自带 4.8、Windows 11 新版本自带 4.8/4.8.1；同时也明确建议新开发优先使用现代 .NET，因此这是一个以 v1 体积约束换取旧框架维护成本的有意取舍：<https://learn.microsoft.com/en-us/dotnet/framework/get-started/system-requirements>。

### 方案 B：Tauri 2 + Rust + WebView2

Rust 承担系统集成，HTML/CSS/TypeScript 实现界面，复用系统 WebView2。Tauri 2 官方配置直接提供透明窗口、always-on-top、skip-taskbar、托盘等能力，也有全局快捷键、单实例、自启等插件：<https://v2.tauri.app/reference/config/>。

### 方案 C：原生 C++ Win32 + Direct2D/GDI

直接创建 Win32 窗口、托盘、消息循环和物理像素渲染；HTTP 使用 WinHTTP，数据层和设置 UI 自行实现。

### 逐项比较

| PRD 约束 | A：.NET Framework WinForms + Win32 | B：Tauri 2 + WebView2 | C：原生 C++ Win32 |
|---|---|---|---|
| 13：安装包约 8–15 MB | **最有把握。** 系统运行时不随包分发；本次单文件 spike 为 24,064 B，正式体积主要来自代码、SQLite 与素材。安装器尚未实测。 | 通常可把安装器压在目标附近，因为不捆 Chromium；但要处理 WebView2 缺失时的安装策略，离线 WebView2 会显著增大包。 | **最小。** 静态链接运行库后仍通常远低于目标，但需要额外实现大量基础设施。 |
| 13：常驻内存约 60–100 MB | 实测 spike 平均约 32 MB、峰值约 33 MB，低于上限，给待办/UI/SQLite 留有余量。 | WebView2 至少包含浏览器/渲染进程，最容易逼近或超过 100 MB；必须另做 spike 才能确认。 | **最低且可控。** |
| 13：待机 CPU ≤ 0.5%，隐藏暂停 | 8 FPS spike 正式数据见“实测数据”；隐藏时停止 Timer，自动核对帧数增量。 | 浏览器合成、CSS 动画和多进程调度风险更高；虽然隐藏页会被节流，但仍需实际测量。 | **最可控。** 手写消息循环与定时器即可。 |
| 6.1：透明、局部点击穿透、置顶 | 已用“跨进程下层窗口 + 窗口区域”实测。命中区域由宠物/清单的实际几何并集组成，空白根本不属于 HWND。 | 透明和置顶有官方配置；HTML 透明像素的逐像素命中仍需 Windows 原生扩展/窗口区域，纯 Web 层不够。 | 原生能力最完整，但实现量最大。 |
| 6.1：多屏和不同缩放 | PMv2 能看到显示器原始像素且不会被 Windows 位图缩放；需处理 `WM_DPICHANGED`。Microsoft 说明见 <https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows>。本机真实 DPI 覆盖不足，见下文。 | Tauri 暴露逻辑像素与 scale factor；WebView/CSS/Windows 合成链更长，像素清晰和命中换算风险较高。 | PMv2 + `WM_DPICHANGED` 全手工处理，控制力最高。 |
| 6.4：多档尺寸像素边缘清晰 | 已用 2×/3×/4× 整数物理像素渲染、完整物理分辨率截图和逐像素调色板检查验证。 | 可用 `image-rendering: pixelated`，但 CSS 尺寸、页面缩放、显示 DPI 与 compositor 叠加后仍可能出现非整数采样。 | Direct2D/GDI 最近邻整数倍率可稳定满足。 |
| 6.2、4：托盘、全局快捷键、全屏检测、单实例、自启 | `NotifyIcon`、`RegisterHotKey`、前台窗口/显示器 API、命名 Mutex、HKCU Run 均可直接实现；Windows 专用项目没有跨平台包袱。 | 托盘、global-shortcut、single-instance、autostart 有官方插件；全屏检测仍要 Windows/Rust 原生代码。 | 全部是原生 API，可靠但样板代码最多。 |
| 11：API Key 系统安全存储 | P/Invoke `CredWriteW` / `CredReadW` 写 Windows Credential Manager；官方说明：<https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credwritea>。不进配置、日志或数据库。 | 需要 Rust keyring/Windows Credential Manager 插件；可行，但要审计第三方依赖。Tauri Stronghold 是应用保险库，不等同于直接使用 Windows 凭据管理器。 | 直接调用 CredWrite/CredRead，最少依赖。 |
| 8、9：OpenAI 兼容接口 | `HttpClient` + JSON 序列化即可；流式响应可用 `HttpCompletionOption.ResponseHeadersRead`。 | Rust `reqwest` 或前端 fetch 均可；需确保 key 不进入 WebView/日志，最好全部请求留在 Rust 侧。 | WinHTTP 可行，但 SSE/JSON/取消/超时的实现与测试成本最高。 |
| 开发与维护成本 | **中等。** 设置页/表单成熟；桌宠核心只在少数 Win32 边界写互操作。缺点是 Microsoft 对新开发更推荐现代 .NET。 | UI 开发最快，但 Rust + Web + WebView2 三层调试，资源与像素链风险最大。 | **最高。** 对当前自用 v1 不划算，且无障碍、中文输入、表单开发成本明显。 |

## 实测数据

### 环境

- Windows build 26200.9445（注册表产品名仍显示 Windows 10，build 对应当前 Windows 11 分支）。
- 16 个逻辑处理器。
- Computer Use 冒烟：当前 Codex 会话只暴露浏览器控制，没有 Windows 原生应用枚举/启动/绑定接口，因而**不能**打开、观察或控制记事本。后续目视检查不能依赖当前会话的 Computer Use。
- 沙箱外真实桌面：1 台显示器，3072×1920，192 DPI（200%）。沙箱内看到的 1536×960 / 96 DPI 是虚拟化视图，不作为 M0 真实环境记录。
- 本机没有 `dotnet`、Rust 或 MSVC 工具链；系统有 .NET Framework 4.x `csc.exe`，因此首选方案可以零安装完成 spike。

### 方法与复现

- 代码和脚本：`spikes/`。
- 一次运行全部正式测试：`.\spikes\run-all.ps1`（约 11 分钟）。
- JSON 与截图：`spikes/evidence/2026-09-20/`。
- 每项自动判定既给 `functionalPass`，也单独给 `hardwareCoverage`；不存在的真实 DPI 不会被模拟场景伪装成已覆盖。

### 结果

| 验证 | 结果 | 数据 |
|---|---|---|
| 点击穿透 | 功能通过；指定 DPI 真实硬件覆盖不足 | 100% 和 150% 模拟几何场景各 12/12 次空白点击落到另一个进程的下层窗口，2/2 次精灵点击由置顶窗口收到；检测到的唯一真实显示器为 200%。 |
| 像素清晰 | 功能通过；指定 DPI 真实硬件覆盖不足 | 100% / 125% / 150% 布局 × 2× / 3× / 4× 尺寸，共 9 张全分辨率截图；每张精灵区域中间色数量 0、异常像素 0。三种指定缩放均不是当前真实显示器缩放。 |
| 待机 CPU | 通过 | 8 FPS 连续 600.005 秒：4,791 帧，平均 CPU 0.1795%，平均 Working Set 37.93 MB、峰值 39.80 MB；隐藏 30.042 秒：帧数增量 0，平均 CPU 0.0098%。 |

## 结论

提议采用**方案 A：C# / .NET Framework 4.8 + Windows Forms + 少量 Win32 互操作**。

理由是它在不安装额外 SDK 的当前机器上已经验证了最危险的两个桌面窗口行为，并给出明显低于资源上限的预跑数据；同时能直接使用 Windows Credential Manager，预计安装包也最容易落在 8–15 MB。方案 C 的资源上限更好，但开发成本不符合自用 v1；方案 B 的开发体验好，但 WebView2 的内存、透明命中和像素缩放都增加了本项目最在意的风险。

本提议未获用户确认前不进入 M1。

## 影响

- 安装包不携带 .NET 运行时，只支持 Windows 10/11 上已安装的 .NET Framework 4.8/4.8.1。
- 桌宠窗口坐标统一以 PMv2 下的物理像素为最终基准；尺寸档位只使用整数像素倍率。普通设置窗口可使用 WinForms 自动布局。
- 透明空白区使用窗口区域裁剪，而不是给整个窗口设置 `WS_EX_TRANSPARENT`；每次清单展开、折叠或布局翻转都要重建区域。
- API Key 只通过 Windows Credential Manager 读写；待办内容和 key 禁止进入日志。
- 正式安装器体积、完整功能后的常驻内存和 1 小时 CPU 仍按 M4 / PRD 18.2 复测。
- 不修改 PRD。真实 100% / 125% / 150% 与多显示器覆盖不足，以及素材未选定，均写入 `docs/PROGRESS.md` 的“待用户确认”；在解决前 M0 不标记完成。
