<div align="center">

# TS6 Speaker Overlay

**A lightweight, high-performance voice overlay tool for TeamSpeak 6.**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()

<!-- 语言切换按钮 / Language Switch -->
<p align="center">
  <a href="#english">
    <img src="https://img.shields.io/badge/Language-English-blue?style=for-the-badge" alt="English">
  </a>
  <a href="#chinese">
    <img src="https://img.shields.io/badge/语言-中文-red?style=for-the-badge" alt="Chinese">
  </a>
</p>

</div>

---

<a id="english"></a>

##  English Description

###  Key Features

- **True Click-Through**: The overlay acts like "air". Mouse clicks pass directly through to the game below without any interference.
- **Ultra-Low Resource Usage**: Built with **.NET 10 Native AOT**. No heavy browser engines (Electron/CEF), resulting in minimal memory footprint.
- **Voice Activation**: Automatically highlights names when speaking; dims or hides them when silent to keep your screen clean.
- **Privacy Focused**: Only shows users in your **current channel** to avoid cluttering your screen with the entire server list.
- **Seamless Auto-Connect**: Auto-saves your API Key after the first authorization. Subsequent launches reconnect to TeamSpeak 6 instantly and silently.

###  Tech Stack

- **Core**: C# 13 / .NET 10 (Preview)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Protocol**: WebSockets (System.Net.WebSockets)
- **Dependencies**: 
  - \Websocket.Client\ (Robust reconnection)
  - \CommunityToolkit.Mvvm\ (Efficient MVVM pattern)
  - \System.Text.Json\ (High-performance JSON parsing)

###  How to Use

1. **Preparation**:
   - Ensure **TeamSpeak 6** client is running.
   - Go to TS6 Settings and ensure **Remote Apps** is enabled (usually on by default).

2. **Run the Program**:
   - Download and run \TS6-SpeakerOverlay.exe\.
   - **First Time Only**: TS6 will prompt a connection request. Click **"Allow"**.

3. **Controls**:
   - **[Unlock Mode]**: Default on startup. Click and hold the black background to drag the overlay to your desired position.
   - **[Lock Mode]**: Once positioned, press **\Ctrl + L\**. The window will lock in place and enable **Click-Through** mode. You can now play your game.

###  Notes

- The program generates an \pikey.txt\ file in its directory to save your authorization. **Do not share this file.**
- If you move the EXE to a new folder and it fails to connect, simply delete the old \pikey.txt\ and re-authorize.

###  License

MIT License

---

<a id="chinese"></a>

##  中文说明 (Chinese)

###  核心功能

- ** 真正的鼠标穿透**：悬浮窗如同空气一般，鼠标点击直接穿透至下方游戏，绝不干扰操作。
- ** 极低资源占用**：基于 **.NET 10 Native AOT** 编译，无浏览器内核，内存占用极低。
- ** 智能声控显示**：说话时名字高亮变色，无人说话时自动半透明/隐藏。
- ** 隐私保护设计**：只显示和你处在**同一个频道**的用户，避免公屏刷屏。
- ** 自动无感连接**：首次授权后自动保存 Key，后续启动自动重连 TeamSpeak 6。

###  技术栈

- **核心语言**: C# 13 / .NET 10 (Preview)
- **UI 框架**: WPF (Windows Presentation Foundation)
- **通信协议**: WebSockets (System.Net.WebSockets)
- **依赖库**: 
  - \Websocket.Client\ (稳健的断线重连)
  - \CommunityToolkit.Mvvm\ (高效 MVVM 模式)
  - \System.Text.Json\ (高性能 JSON 解析)

###  如何使用

1. **准备工作**：
   - 确保安装并运行了 **TeamSpeak 6** 客户端。
   - 在 TS6 设置中开启 **Remote Apps** 功能（通常默认开启）。

2. **运行程序**：
   - 下载并运行 \TS6-SpeakerOverlay.exe\。
   - **首次运行**时，TS6 客户端会弹出请求连接的窗口，请点击 **"允许 (Allow)"**。

3. **操作说明**：
   - **[解锁模式]**：启动后默认为解锁状态，按住黑色背景可随意拖拽位置。
   - **[锁定模式]**：位置满意后，按下 **\Ctrl + L\**。此时窗口将**锁定并开启鼠标穿透**，你可以开始游戏了。

###  注意事项

- 程序会在同级目录下生成 \pikey.txt\ 用于保存授权信息，请勿将此文件发送给他人。
- 如果移动了 EXE 文件位置导致无法自动连接，请删除旧的 \pikey.txt\ 并重新授权。

###  开源协议

本项目基于 MIT License 开源。
