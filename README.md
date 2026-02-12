<div align="center">

# TS6 Speaker Overlay

**A lightweight, high-performance voice overlay tool for TeamSpeak 6.**

<!-- Downloads -->
[![Download Latest](https://img.shields.io/github/v/release/beka2nt/TS6-SpeakerOverlay?label=Download%20EXE&style=for-the-badge&color=orange)](https://github.com/beka2nt/TS6-SpeakerOverlay/releases/latest)

<!-- Status -->
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-blue.svg)]()
[![License](https://img.shields.io/badge/License-MIT-green.svg)]()

<p align="center">
  <a href="#english">
    <img src="https://img.shields.io/badge/Language-English-blue?style=flat-square" alt="English">
  </a>
  <a href="#chinese">
    <img src="https://img.shields.io/badge/语言-中文-red?style=flat-square" alt="Chinese">
  </a>
</p>

</div>

---

<a id="english"></a>

## 🇬🇧 English Description

### 🚀 What's New in v1.4.x
- **Config Migration (v1.4.3)**: Moved `config.json` and `apikey.txt` to the Windows AppData folder (`%AppData%\TS6-SpeakerOverlay`). This ensures settings are saved correctly even when the app is installed in protected directories like `C:\Program Files`.
- **Persistent Lock State**: The overlay now remembers your "Locked" (Click-Through) status between launches.
- **Enhanced Performance**: Optimized data synchronization with the TS6 client, reducing update latency to 0.2s.
- **Expanded Localization**: Added support for **Russian (Русский)** alongside English, Chinese, and French.

### ✨ Key Features
- **Mouse Click-Through**: Implements Windows API to allow mouse events to pass directly to the game, ensuring uninterrupted gameplay.
- **System Tray Integration**: Supports minimizing to the tray with a full context menu for state management.
- **Visual Feedback**: High-quality vector icons for Mute, Deafen, and Away statuses, plus toast notifications for channel events.
- **Native Architecture**: Built with **.NET 10 Native AOT** for minimal memory footprint and instant startup.

### 📦 How to Use
1. **Download**: Click the **Download EXE** badge above.
2. **Launch**: Run `TS6-SpeakerOverlay.exe`. (Recommend "Run as Administrator" for games with Anti-Cheat like EAC).
3. **Authorize**: Click **"Allow"** in your TeamSpeak 6 client.
4. **Configure**: Right-click the tray icon to open the Settings menu.

---

<a id="chinese"></a>

## 🇨🇳 中文说明 (Chinese)

### 🚀 v1.4.x 更新摘要
- **配置路径迁移 (v1.4.3)**：将 `config.json` 与 `apikey.txt` 迁移至系统应用数据目录 (`%AppData%\TS6-SpeakerOverlay`)。彻底解决了程序安装在 `C:\Program Files` 等受保护目录下时无法保存设置的权限问题。
- **锁定状态记忆**：程序现在会记录“锁定/穿透”状态，重启后无需重新手动锁定。
- **性能大幅优化**：重构了与 TS6 客户端的数据同步逻辑，状态更新延迟缩短至 0.2 秒。
- **多语言扩展**：新增 **俄语 (Русский)** 支持。

### ✨ 核心功能
- **鼠标事件穿透**：基于 Windows API 实现，确保悬浮窗在锁定模式下不干扰任何游戏操作。
- **系统托盘集成**：支持最小化至托盘运行，右键菜单提供完整的控制选项。
- **状态可视化**：采用高清矢量图标显示成员状态（闭麦、静音、离开），并提供进出频道的气泡通知。
- **原生 AOT 编译**：基于 **.NET 10** 构建，极致轻量，无浏览器内核，极低资源占用。

### 📦 使用指南
1. **下载程序**：点击顶部的 **Download EXE** 按钮获取最新版本。
2. **运行配置**：双击运行程序。若游戏开启了 EAC 等反作弊系统，建议**以管理员身份运行**。
3. **授权连接**：在 TeamSpeak 6 客户端弹出的请求中选择 **"允许 (Allow)"**。
4. **调整设置**：右键点击任务栏右下角的托盘图标即可打开设置面板，自定义外观与行为。

### 📄 License
MIT License
