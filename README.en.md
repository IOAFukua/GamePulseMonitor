# GamePulseMonitor

Language: [中文](README.md) | English

<p align="center">
  <img src="https://img.shields.io/github/v/release/IOAFukua/GamePulseMonitor?style=flat-square&label=Version&color=10b981" alt="Version">
  <img src="https://img.shields.io/github/license/IOAFukua/GamePulseMonitor?style=flat-square&label=License&color=10b981" alt="License">
  <img src="https://img.shields.io/badge/.NET-8.0-blue?style=flat-square&logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/platform-Windows-blue?style=flat-square&logo=windows" alt="Windows">
  <img src="https://img.shields.io/github/stars/IOAFukua/GamePulseMonitor?style=flat-square&label=Stars" alt="Stars">
  <img src="https://img.shields.io/github/downloads/IOAFukua/GamePulseMonitor/total?style=flat-square&label=Downloads" alt="Downloads">
</p>

> 🎮 **Lightweight Windows in-game performance monitor overlay** · Real-time FPS, CPU, GPU, VRAM, RAM tracking, plus screenshot tool & pinned overlay!

🌐 **[Official Website](https://www.gameicu.online)** · Features, screenshots and downloads

---

## ⚡ Features

| Feature | Description |
|---------|-------------|
| 🎯 **FPS Monitor** | Accurate FPS capture via PresentMon 1.9.2 |
| 📊 **Benchmark Stats** | `Alt+A` avg FPS and 1% low FPS |
| 🖥️ **System Resources** | CPU / GPU / VRAM / RAM real-time data |
| 📈 **History Charts** | Per-date/per-process, Ctrl+wheel zoom |
| ✂️ **Screenshot Region** | **New** In-game screenshot with annotation |
| 📌 **Pinned Screenshot** | **New** Pin screenshot on top, Alt+drag to move |
| 🎯 **Click-Through** | Doesn't steal game input |
| 📝 **CSV Logging** | 1s interval recording |
| 🌐 **Bilingual** | Chinese / English UI |
| ⚙️ **Customizable** | Hotkeys, fields, colors, auto-start |

---

## 📥 Download

[![Latest Release](https://img.shields.io/badge/Download-Latest_Release-10b981?style=for-the-badge&logo=github)](https://github.com/IOAFukua/GamePulseMonitor/releases/latest)

| Package | Description | Size |
|---------|-------------|------|
| `GamePulseMonitorSetup-Lite.exe` | Lite installer, needs .NET 8 Desktop Runtime | ~1 MB |
| `GamePulseMonitorSetup.exe` | Full offline package | ~61 MB |
| `GamePulseMonitor-portable-lite-win-x64.zip` | Lite portable | ~0.7 MB |
| `GamePulseMonitor-portable-win-x64.zip` | Full portable | ~62 MB |

Winget:
```
winget install IOAFukua.GamePulseMonitor
```

---

## 🚀 Quick Start

```powershell
# Dev run
.\scripts\run.ps1

# If PowerShell execution policy blocks
.\scripts\run.cmd

# Lock to a specific process
.\scripts\run.ps1 -ProcessName game.exe

# Custom position
.\scripts\run.ps1 -ProcessName game.exe -Left 32 -Top 32
```

---

## ⌨️ Hotkeys

| Hotkey | Action |
|--------|--------|
| `Alt+A` | Start/stop avg FPS benchmark |
| `Ctrl+Shift+S` | **New** Screenshot region capture |
| `Ctrl+Shift+F11` | Show/hide overlay |
| `Ctrl+Shift+F12` | Exit |
| `Alt+Drag` | Move overlay |
| `Ctrl+Wheel` | Zoom history chart |
| Double-click pin | **New** Destroy pinned screenshot |

---

## 🏗️ Building

```powershell
# Publish
.\scripts\publish.ps1  # or .\scripts\publish.cmd

# Build installer
.\scripts\build-installer.ps1  # or .\scripts\build-installer.cmd
```

Output: `artifacts/GamePulseMonitor/`

---

## 📄 License

MIT License — see [LICENSE](LICENSE).
