<div align="center">

# TS6 Speaker Overlay

**A lightweight, high-performance voice overlay tool for TeamSpeak 6.**

[![Download Latest](https://img.shields.io/github/v/release/beka2nt/TS6-SpeakerOverlay?label=Download%20EXE&style=for-the-badge&color=orange)](https://github.com/beka2nt/TS6-SpeakerOverlay/releases/latest)

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

##  English Description

###  What's New in v1.4.0
- **Instant Response**: Optimized data synchronization logic. Channel switching and status updates are now nearly instantaneous (0.2s latency).
- **Interactive Drag Handle**: A visual frame now appears when hovering over the overlay in unlocked mode, making it easier to position.
- **Russian Support**: Added **Русский** language option (Thanks to community contribution).
- **Smart Disconnect**: The overlay now correctly clears the user list and shows "Waiting for Server" immediately when disconnecting from a TS server.
- **Bug Fixes**: Fixed issues with manual refresh not working and settings binding errors.

###  Key Features
- **True Click-Through**: Passes mouse events directly to the game.
- **Avatar Display Modes**: Avatar / Indicator Only / Name Only.
- **Settings GUI**: Real-time customization for Size, Opacity, and Spacing.
- **Visual Notifications**: Toast popups for join/leave events.
- **Ultra-Low Resource**: Native .NET 10 AOT, minimal memory footprint.

###  How to Use
1. **Download**: Click the **Download EXE** badge above.
2. **Run**: Launch `TS6-SpeakerOverlay.exe`. Allow connection in TS6 client.
3. **Settings**: Right-click the tray icon -> **Settings**.
4. **Lock**: Press **`Ctrl + L`** to lock position and enable click-through.

###  License
MIT License

---

<a id="chinese"></a>

##  中文说明 (Chinese)

###  v1.4.0 更新日志
- **极速响应模式**：重构了数据同步逻辑，频道切换与状态更新延迟降低至 0.2 秒，体验如丝般顺滑。
- **交互式拖拽框**：在解锁模式下，鼠标悬停时会显示动态边框与提示图标，调整位置更直观。
- **俄语支持**：新增 **Русский** 语言支持。
- **智能断线检测**：修复了退出服务器后列表不消失的问题。现在断开连接会立即清空列表并提示等待加入服务器。
- **问题修复**：修复了设置界面下拉框绑定失效及手动刷新无效的问题。

###  核心功能
- **鼠标穿透**：悬浮窗不拦截点击，完美覆盖于游戏之上。
- **头像模式**：支持显示真实头像、仅指示灯或极简文字模式。
- **可视化设置**：右键托盘可打开设置面板，调整大小、透明度、间距等。
- **进出通知**：成员进出频道时弹出气泡提示。
- **极低占用**：原生 AOT 编译，无浏览器内核，性能极致。

###  使用指南
1. **下载**：点击顶部的 **Download EXE** 下载最新版。
2. **运行**：双击运行，在 TS6 中点击允许连接。
3. **设置**：右键托盘图标 -> **设置 (Settings)**，可切换语言。
4. **锁定**：按 **`Ctrl + L`** 锁定位置并开启穿透模式。

###  开源协议
本项目基于 MIT License 开源。
