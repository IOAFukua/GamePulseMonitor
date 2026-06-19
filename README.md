# GamePulseMonitor

语言：中文 | [English](README.en.md)

<p align="center">
  <img src="https://img.shields.io/github/v/release/IOAFukua/GamePulseMonitor?style=flat-square&label=版本&color=10b981" alt="Version">
  <img src="https://img.shields.io/github/license/IOAFukua/GamePulseMonitor?style=flat-square&label=许可&color=10b981" alt="License">
  <img src="https://img.shields.io/badge/.NET-8.0-blue?style=flat-square&logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/platform-Windows-blue?style=flat-square&logo=windows" alt="Windows">
  <img src="https://img.shields.io/github/stars/IOAFukua/GamePulseMonitor?style=flat-square&label=Stars" alt="Stars">
  <img src="https://img.shields.io/github/downloads/IOAFukua/GamePulseMonitor/total?style=flat-square&label=下载" alt="Downloads">
</p>

> 🎮 **轻量级 Windows 游戏内性能监控悬浮窗** · 实时追踪 FPS、CPU、GPU、显存与内存，还带截图工具和悬浮钉图！

🌐 **[官方主页](https://www.gameicu.online)** · 查看功能介绍、截图和下载

---

## 📸 预览

### 实时悬浮窗
```
┌──────────────────────────────────────┐
│  FPS                        144.0    │
│  Avg                        141.2    │
│  1% Low                     98.0     │
│  ───────────────────────────         │
│  Frame Time                 6.9 ms   │
│  ───────────────────────────         │
│  CPU          ████████▌     34%      │
│  GPU          ████████████▌ 72%      │
│  VRAM         █████▋        4.2/8.0  │
│  RAM          ████████████▌ 12.8 GB  │
└──────────────────────────────────────┘
```

### ✂️ 截图选区 & 悬浮钉图（v0.2.0 新功能）
游戏内按 `Ctrl+Shift+S` 唤起截图，拖拽选区和标注工具，一键钉在屏幕上。支持画笔、箭头、文字、马赛克等标注。

---

## ⚡ 核心功能

| 功能 | 说明 |
|------|------|
| 🎯 **FPS 监控** | 基于 PresentMon 1.9.2 的精准 FPS 采集 |
| 📊 **基准统计** | `Alt+A` 统计平均 FPS 和 1% Low FPS |
| 🖥️ **系统资源** | CPU / GPU / VRAM / RAM 实时数据 |
| 📈 **历史曲线** | 按日期和进程查看，Ctrl+滚轮缩放，节点详情 |
| ✂️ **截图选区** | **新版** 游戏内截图，拖拽选区，自由标注 |
| 📌 **悬浮钉图** | **新版** 截图钉在屏幕最上层，Alt+拖动调整 |
| 🎯 **鼠标穿透** | 不抢占游戏输入，Alt 激活交互 |
| 📝 **CSV 日志** | 每秒记录，方便后期分析 |
| 🌐 **多语言** | 中文 / English 界面切换 |
| ⚙️ **自定义** | 快捷键、字段显隐、颜色、开机自启 |

---

## 📥 下载

[![最新版本](https://img.shields.io/badge/下载-最新版本-10b981?style=for-the-badge&logo=github)](https://github.com/IOAFukua/GamePulseMonitor/releases/latest)

| 安装包 | 说明 | 大小 |
|--------|------|------|
| `GamePulseMonitorSetup-Lite.exe` | 轻量安装包，需 .NET 8 Desktop Runtime | ~1 MB |
| `GamePulseMonitorSetup.exe` | 完整离线包，无需预装 .NET | ~61 MB |
| `GamePulseMonitor-portable-lite-win-x64.zip` | 轻量便携版 | ~0.7 MB |
| `GamePulseMonitor-portable-win-x64.zip` | 完整便携版 | ~62 MB |

也可以通过 Winget 安装：
```
winget install IOAFukua.GamePulseMonitor
```

---

## 🚀 快速开始

```powershell
# 开发运行
.\scripts\run.ps1

# 如果 PowerShell 策略阻止
.\scripts\run.cmd

# 锁定到指定进程
.\scripts\run.ps1 -ProcessName game.exe

# 指定位置
.\scripts\run.ps1 -ProcessName game.exe -Left 32 -Top 32
```

---

## ⌨️ 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Alt+A` | 开始/停止平均 FPS 统计 |
| `Ctrl+Shift+S` | **新版** 唤起截图选区 |
| `Ctrl+Shift+F11` | 显示/隐藏悬浮窗 |
| `Ctrl+Shift+F12` | 退出程序 |
| `Alt+拖动` | 移动悬浮窗位置 |
| `Ctrl+滚轮` | 历史曲线缩放 |
| 双击钉图 | **新版** 销毁截图 |

---

## 🏗️ 从源码构建

### 环境要求
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 (推荐)

### 构建步骤

```powershell
# 发布
.\scripts\publish.ps1
# 或
.\scripts\publish.cmd

# 构建安装包
.\scripts\build-installer.ps1
# 或
.\scripts\build-installer.cmd
```

构建产物会输出到 `artifacts/GamePulseMonitor` 目录。

---

## 📁 项目结构

```
GamePulseMonitor/
├── src/
│   └── GamePulseMonitor/          # 主程序 (WPF)
├── scripts/                       # 构建/运行脚本
│   ├── run.ps1 / run.cmd
│   ├── publish.ps1 / publish.cmd
│   └── build-installer.ps1 / .cmd
├── tools/
│   ├── presentmon/                # PresentMon 组件
│   └── presentmon1/
├── CHANGELOG.md                   # 版本更新日志
├── CONTRIBUTING.md                # 贡献指南
├── LICENSE                        # MIT 许可证
├── README.md / README.en.md       # 中英文文档
└── GamePulseMonitor.sln           # 解决方案文件
```

---

## 🤝 贡献

欢迎贡献代码、报告 Bug 或提出新功能！

- [提交 Bug 报告](https://github.com/IOAFukua/GamePulseMonitor/issues/new?template=bug_report.md)
- [提出功能建议](https://github.com/IOAFukua/GamePulseMonitor/issues/new?template=feature_request.md)
- 查阅 [CONTRIBUTING.md](CONTRIBUTING.md) 了解开发指引

---

## 📄 许可

本项目采用 MIT 许可证 — 详见 [LICENSE](LICENSE) 文件。
