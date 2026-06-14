# GamePulseMonitor

语言：中文 | [English](README.en.md)

GamePulseMonitor 是一个基于 C#/.NET WPF 开发的轻量级 Windows 游戏内性能监控悬浮窗。

软件每 1 秒采样一次，并记录：

- FPS，来自内置 PresentMon 1.9.2 采集器
- `Alt+A` 基准统计窗口内的平均 FPS 和 1% Low FPS
- 当前秒的平均帧时
- 总 CPU 占用率和目标进程 CPU 占用率
- Windows GPU Engine 计数器提供的总 GPU 占用率
- 独立/共享显存占用，包括 Windows 可提供时的目标进程显存占用
- 系统内存占用

## 下载

请在 GitHub Release 页面下载安装包：

[最新版本](https://github.com/IOAFukua/GamePulseMonitor/releases/latest)

推荐选择：

- `GamePulseMonitorSetup-Lite.exe`：体积小，适合已经安装 Microsoft .NET 8 Desktop Runtime 的电脑
- `GamePulseMonitorSetup.exe`：完整离线包，适合分享给没有安装 .NET 环境的电脑

安装程序会在安装前让用户选择安装目录，默认目录是 `D:\GamePulseMonitor`，并会创建桌面和开始菜单快捷方式。

## 运行

开发环境中可以直接运行：

```powershell
.\scripts\run.ps1
```

如果 PowerShell 脚本执行被系统策略阻止，可以使用：

```powershell
.\scripts\run.cmd
```

软件会请求管理员权限，因为实时 PresentMon/ETW FPS 采集在 Windows 上需要提升权限。

默认会跟随当前前台进程。也可以锁定到指定游戏进程：

```powershell
.\scripts\run.ps1 -ProcessName game.exe
```

常用参数：

```powershell
.\scripts\run.ps1 -ProcessName game.exe -Left 32 -Top 32
.\scripts\run.ps1 -NoClickThrough
```

## 快捷键

- `Alt+A`：开始或停止平均 FPS / 1% Low 统计窗口
- `Ctrl+Shift+F11`：显示或隐藏悬浮窗
- `Ctrl+Shift+F12`：退出程序

右键托盘图标并选择“设置”，可以开启或关闭开机自启动、切换语言、配置快捷键，以及选择悬浮窗字段的显示与隐藏。软件默认语言是中文，也内置英文。

平均 FPS 和 1% Low 默认不开启。按一次 `Alt+A` 会清空旧结果并开始统计当前目标；再次按下 `Alt+A` 会停止统计，并保留本次结果显示在界面上。

按住 `Alt` 后，可以用鼠标左键拖动悬浮窗位置。普通状态下悬浮窗保持鼠标穿透，不会抢占游戏输入。

按住 `Alt` 并指向悬浮窗中的某个字段，可以查看该字段数值的详细说明。

托盘菜单还提供显示/隐藏和退出操作。

## 曲线图

历史曲线数据会保存在本地，用户可以按日期和进程查看。

- 实时模式：显示最近 3 分钟数据，并随采样实时刷新
- 历史模式：按用户选择的日期、进程和时间区间查看，不自动刷新
- `Ctrl + 鼠标滚轮`：在曲线图上放大或缩小时间范围
- 鼠标指向曲线节点：显示该节点的具体时间和值

## 日志

CSV 日志每秒写入一次。开发运行时默认位于：

```text
src\GamePulseMonitor\bin\Release\net8.0-windows10.0.17763.0\logs
```

使用发布后的程序时，日志会写入 `GamePulseMonitor.exe` 同级目录下的 `logs` 文件夹。

## 发布

```powershell
.\scripts\publish.ps1
```

或：

```powershell
.\scripts\publish.cmd
```

发布文件会输出到：

```text
artifacts\GamePulseMonitor
```

## 构建安装包

```powershell
.\scripts\build-installer.ps1
```

或：

```powershell
.\scripts\build-installer.cmd
```

该脚本会生成 x64 Windows 完整安装包和 portable zip：

```text
artifacts\GamePulseMonitorSetup.exe
artifacts\GamePulseMonitor-portable-win-x64.zip
```

同时也会生成体积更小的框架依赖版：

```text
artifacts\GamePulseMonitorSetup-Lite.exe
artifacts\GamePulseMonitor-portable-lite-win-x64.zip
```

目标电脑已经安装 Microsoft .NET 8 Desktop Runtime 时，建议使用 Lite 包。需要离线分享、无需用户先安装 .NET 时，建议使用完整包。

## 注意事项

悬浮窗是透明置顶窗口，最适合窗口化或无边框全屏游戏。独占全屏游戏可能阻止普通桌面悬浮窗显示，除非游戏或显卡驱动支持在独占模式上叠加桌面组合层。

FPS 采集由内置 Intel PresentMon 控制台采集器完成，路径为 `tools\presentmon\PresentMon.exe`。GamePulseMonitor 优先使用实时 CSV 流中的显示帧时间数据（`MsBetweenDisplayChange` / `DisplayedTime`），当显示时间不可用时会回退到 Present 时间。
