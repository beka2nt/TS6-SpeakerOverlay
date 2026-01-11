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

## 🇬🇧 English Description

### 🚀 What's New in v1.3.0
- **Multi-Language Support**: Switch between **English**, **Chinese**, and **French** instantly in Settings.
- **Avatar Display Modes**: Choose how users are represented:
  - **Avatar**: Shows TS6 avatar (with Initials fallback if missing).
  - **Indicator Only**: Minimalist colored dots.
  - **Name Only**: Just text, ultra-compact.
- **Smart Avatar**: If a user has no cloud avatar, their initials will be shown automatically.

### ✨ Key Features
- **True Click-Through**: Passes mouse events to the game.
- **Settings GUI**: Customize Font Size, Opacity, Spacing, and Scaling in real-time.
- **Visual Notifications**: Toast popups when users join/leave.
- **Ultra-Low Resource**: Native .NET 10 AOT, minimal memory usage.
- **Auto-Connect**: Saves API Key and reconnects automatically.

### 📦 How to Use
1. **Download**: Click the **Download EXE** badge above.
2. **Run**: Launch `TS6-SpeakerOverlay.exe`. Allow connection in TS6 client.
3. **Settings**: Right-click the tray icon -> **Settings**.
4. **Lock**: Press **`Ctrl + L`** to lock position and enable click-through.

### 📄 License
MIT License

---

<a id="chinese"></a>

## 🇨🇳 中文说明 (Chinese)

### 🚀 v1.3.0 重磅更新
- **多语言支持**：原生支持 **中文、英文、法文** 切换，设置即时生效。
- **头像显示模式**：提供三种显示风格供选择：
  - **显示头像**：优先显示 TeamSpeak 云头像，无头像时自动显示名字首字母。
  - **仅指示灯**：只显示极简的彩色圆点。
  - **仅显示名字**：最紧凑的纯文字模式。
- **首字母兜底**：解决了默认头像显示为灰色的问题，现在会自动生成带背景色的首字母图标。

### ✨ 核心功能
- **鼠标穿透**：悬浮窗不拦截点击，完美覆盖于游戏之上。
- **可视化设置**：右键托盘可打开设置面板，调整大小、透明度、间距等。
- **进出通知**：成员进出频道时弹出气泡提示。
- **极低占用**：原生 AOT 编译，无浏览器内核，性能极致。
- **自动连接**：首次授权后自动保存 Key，开机即用。

### 📦 使用指南
1. **下载**：点击顶部的 **Download EXE** 下载最新版。
2. **运行**：双击运行，在 TS6 中点击允许连接。
3. **设置**：右键托盘图标 -> **设置 (Settings)**，可切换语言。
4. **锁定**：按 **`Ctrl + L`** 锁定位置并开启穿透模式。

### 📄 开源协议
本项目基于 MIT License 开源。
