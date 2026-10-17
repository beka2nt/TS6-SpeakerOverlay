# TS6 Speaker Overlay

TeamSpeak 6 的 Windows 语音悬浮窗，查看当前频道成员与发言状态。

[English](#english) · [下载已发布版本](https://github.com/beka2nt/TS6-SpeakerOverlay/releases/latest) · [反馈问题](https://github.com/beka2nt/TS6-SpeakerOverlay/issues)

![Windows x64](https://img.shields.io/badge/Windows-x64-168F76)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![Source version](https://img.shields.io/badge/source-v1.5.0-168F76)

当前源码版本为 **1.5.0**。下载链接指向 GitHub Releases 中最新的已发布版本；也可以按下方说明构建当前源码。

## 界面预览

![设置界面](docs/images/settings.png)

<details>
<summary>查看连接与诊断设置、托盘菜单</summary>

![连接与诊断](docs/images/settings-diagnostics.png)

![托盘菜单](docs/images/tray-menu.png)

</details>

## 功能

- **发言动效与颜色**：说话时边框、声量条与头像光环平滑亮起，支持预设与自定义颜色。
- **四种显示模式**：显示头像、仅头像、仅指示灯、仅名字；分别展示闭麦、关闭声音与离开状态。
- **Discord 式说话模式**：隐藏静默成员，只显示正在说话的人；解锁后保留定位入口。
- **服务器选择**：同时连接多个服务器时，可选择展示来源，头像优先使用所选服务器上的头像。
- **进出通知**：所选服务器成员上线、离线，以及当前频道进入、离开，可分别开关。
- **外观设置**：字体、头像大小、列表间距、背景深度与整体缩放即时生效。
- **鼠标穿透与定位**：通过托盘锁定、解锁、显示、隐藏悬浮窗；解锁后拖动定位。
- **随 TeamSpeak 启动**：可选的后台等待模式，Windows 登录后驻留托盘，检测到 TeamSpeak 启动时显示悬浮窗。
- **应用内日志**：查看最近记录、筛选错误、刷新或复制，也可打开日志文件夹。
- **多语言**：简体中文、English、Français、Русский。

## 开始使用

使用自包含 exe 需要 **Windows x64 和 TeamSpeak 6**，无需单独安装 .NET。从源码构建需要 **.NET 10 SDK**。

1. 启动 TeamSpeak 6，在“设置 → 远程应用程序”中确认接口已启用，并记下端口。
2. 启动悬浮窗，在 TeamSpeak 弹出的授权提示中允许连接。
3. 右键 Windows 托盘图标打开“设置”。向下滚动到“连接与诊断”，将“远程应用端口”设为与 TeamSpeak 一致。**修改端口后重启悬浮窗。**
4. 加入服务器与频道。若连接多个服务器，在设置顶部选择展示来源。

默认端口为 `5899`，设置中提供 `5899 / 6100 / 6200 / 7000` 选项，以你自己的 TeamSpeak 设置为准。

### 移动与锁定

从托盘选择“解锁（拖拽）”，拖动悬浮窗到合适位置，再选择“锁定（穿透）”。悬浮窗获得焦点时也可用 `Ctrl+L` 切换锁定状态。

“显示窗口”和“隐藏窗口”控制悬浮层可见性；“退出程序”会完全退出。设置更改会自动保存。

### 随 TeamSpeak 启动

在“行为设置”开启“随 TeamSpeak 启动显示悬浮窗”，默认关闭。

开启后，软件为当前 Windows 用户登记登录启动项，登录后先在托盘等待，检测到 `TeamSpeak.exe` 后显示悬浮窗。手动打开会直接显示已有窗口。关闭开关会撤销启动登记；从托盘完全退出后，本次后台等待也会停止。

## 日志与本地数据

打开“设置 → 连接与诊断 → 查看最近日志”，可以切换“仅错误”、刷新和复制。查看器最多展示最近五个日志文件中的 200 条匹配记录。

数据保存在 `%LOCALAPPDATA%/TS6-SpeakerOverlay/`：

| 内容 | 文件 |
| --- | --- |
| 外观、服务器选择、端口与启动设置 | `config.json` |
| 使用 Windows 当前用户保护机制（DPAPI）保存的授权密钥 | `apikey.txt` |
| 运行记录与异常详情 | `logs/app-YYYY-MM-DD.log`，轮转文件带编号后缀 |

单个日志文件达到 **3 MB** 后轮转，自动保留最近 **30 个文件**。单条超长记录会截断，`apiKey` 字段会脱敏。旧版程序目录中的配置与密钥会在需要时迁移到本地应用数据目录。

## 常见问题

**一直等待加入服务器或无法连接**

确认 TeamSpeak 正在运行、已加入服务器，并检查远程应用程序接口与端口。端口变更后需要重启悬浮窗；可使用托盘“刷新状态”重新请求状态，并在日志查看器中检查连接或错误记录。

**说话模式下找不到拖动位置**

先从托盘解锁悬浮窗，使用定位入口调整位置，再锁定以开启鼠标穿透。

**同时连接多个服务器**

在设置顶部选择“悬浮窗服务器”。成员列表和进出通知跟随所选服务器及当前频道。

## 从源码构建

在 Windows 上安装 .NET 10 SDK，在仓库根目录运行：

```powershell
dotnet restore TS6-SpeakerOverlay/TS6-SpeakerOverlay.csproj
dotnet build TS6-SpeakerOverlay/TS6-SpeakerOverlay.csproj -c Release
dotnet run --project TS6-SpeakerOverlay/TS6-SpeakerOverlay.csproj
```

打包自包含的单文件 Windows x64 exe：

```powershell
dotnet publish TS6-SpeakerOverlay/TS6-SpeakerOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o outputs/publish
```

生成文件位于 `outputs/publish/TS6-SpeakerOverlay.exe`。构建输出、日志、个人配置和密钥不会作为源码提交。

## 更新记录

| 版本 | 更新 |
| --- | --- |
| 1.5.0 | 统一设置页、托盘和悬浮窗视觉；重排连接与诊断；新增应用内日志查看与大小轮转。 |
| 1.4.6 | 优化托盘图标与文字对齐、字体和图标绘制。 |
| 1.4.5 | 新增随 TeamSpeak 启动显示、后台等待与重复启动唤出。 |
| 1.4.4 | 新增所选服务器及当前频道进出通知，可分别关闭。 |
| 1.4.3 | 可配置远程应用端口，改进连接状态提示。 |
| 1.4.2 | 新增说话亮灯颜色选择，改进频道成员刷新。 |

<a id="english"></a>

## English

TS6 Speaker Overlay is a Windows x64 voice overlay for TeamSpeak 6. The current source version is **1.5.0**.

### Features

- Smooth speaking highlights with presets and a custom color picker.
- Avatar, avatar-only, indicator-only and name-only modes, with microphone, audio and away indicators.
- Discord-style speaker mode that hides silent members.
- Select a connected server and prefer its server-specific avatars.
- Independent server connection/disconnection and current-channel join/leave notifications.
- Adjustable font size, avatar size, spacing, opacity and scale.
- Tray controls for positioning, click-through, visibility and exit.
- Optional Windows sign-in startup that waits until TeamSpeak launches.
- In-app logs with error filtering, refresh and copy.
- Chinese, English, French and Russian interfaces.

### Setup and controls

1. Run TeamSpeak 6 and enable its Remote Apps interface.
2. Launch the overlay and allow its connection in TeamSpeak.
3. Open **Settings** from the tray. Under connection and diagnostics, choose the same Remote Apps port as TeamSpeak. Restart the overlay after changing the port.
4. Join a server/channel, then select the server to display at the top of Settings.

The default port is `5899`; Settings offers `5899 / 6100 / 6200 / 7000`. Use the value configured in your TeamSpeak client.

Unlock from the tray to drag the overlay, then lock it for click-through. `Ctrl+L` also works when the overlay has focus. Changes save automatically.

Enable **Show overlay when TeamSpeak starts** to register current-user Windows sign-in startup. The app waits in the tray and shows the overlay when `TeamSpeak.exe` is detected. Disabling the option removes that registration. Exiting the app stops the current background wait.

### Logs and builds

Open **Settings → View recent logs** to filter errors, refresh or copy. The viewer shows up to 200 matching entries from the five latest log files. Data is stored under `%LOCALAPPDATA%/TS6-SpeakerOverlay/`; logs rotate at 3 MB per file, keeping the latest 30 files. API key fields are redacted, and the saved authorization key is protected with Windows DPAPI for the current user.

Download a published executable from [GitHub Releases](https://github.com/beka2nt/TS6-SpeakerOverlay/releases/latest). The self-contained build needs no separate .NET installation. To build current source, use Windows and .NET 10 SDK with the commands in [从源码构建](#从源码构建).

## License

MIT.
