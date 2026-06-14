# GamePulseMonitor

GamePulseMonitor is a lightweight Windows in-game monitor overlay built with C#/.NET WPF.

It samples once per second and records:

- FPS from the bundled PresentMon 1.9.2 collector
- Average FPS and 1% low FPS for an Alt+A benchmark window
- Average frame time for the current second
- Total CPU usage and target process CPU usage
- Total GPU usage from Windows GPU Engine counters
- Dedicated/shared VRAM usage, including target-process VRAM when Windows exposes it
- System RAM usage

## Run

```powershell
.\scripts\run.ps1
```

If PowerShell script execution is blocked, use:

```powershell
.\scripts\run.cmd
```

The app requests administrator permission because real-time PresentMon/ETW FPS capture needs elevated access on Windows.

By default it follows the current foreground process. To lock it to a game:

```powershell
.\scripts\run.ps1 -ProcessName game.exe
```

Useful options:

```powershell
.\scripts\run.ps1 -ProcessName game.exe -Left 32 -Top 32
.\scripts\run.ps1 -NoClickThrough
```

Hotkeys:

- `Alt+A`: start or stop the average FPS / 1% low benchmark window
- `Ctrl+Shift+F11`: show or hide the overlay
- `Ctrl+Shift+F12`: exit

Right-click the tray icon and choose `Settings` to toggle Windows startup, switch language, configure hotkeys, and choose which overlay fields are visible. Chinese is the default language; English is also available.

Average FPS and 1% low are disabled by default. Press `Alt+A` once to clear previous values and start measuring the current target; press `Alt+A` again to stop and keep the result on screen.

Hold `Alt` and drag the overlay with the left mouse button to move it. In normal mode the overlay remains click-through so it does not steal game input.

Hold `Alt` and point at an overlay field to see a detailed explanation of that value.

The tray icon also has show/hide and exit actions.

## Logs

CSV logs are written once per second under:

```text
src\GamePulseMonitor\bin\Release\net8.0-windows10.0.17763.0\logs
```

When using published output, logs are written to the `logs` folder next to `GamePulseMonitor.exe`.

## Publish

```powershell
.\scripts\publish.ps1
```

or:

```powershell
.\scripts\publish.cmd
```

Published files are placed in:

```text
artifacts\GamePulseMonitor
```

## Build Installer

```powershell
.\scripts\build-installer.ps1
```

or:

```powershell
.\scripts\build-installer.cmd
```

This creates a self-contained x64 Windows installer and a portable zip:

```text
artifacts\GamePulseMonitorSetup.exe
artifacts\GamePulseMonitor-portable-win-x64.zip
```

It also creates a small framework-dependent package:

```text
artifacts\GamePulseMonitorSetup-Lite.exe
artifacts\GamePulseMonitor-portable-lite-win-x64.zip
```

Use the Lite package when the target PC already has Microsoft .NET 8 Desktop Runtime installed. Use the normal package for offline sharing without installing .NET first.

The installer asks for an install folder before copying files. The default folder is `D:\GamePulseMonitor`. It also creates desktop and Start Menu shortcuts.

## Notes

The overlay is a transparent topmost window, so it works best with windowed or borderless fullscreen games. Exclusive fullscreen games can prevent normal desktop overlays from appearing unless the game or driver supports composition over exclusive mode.

FPS capture is handled by the bundled Intel PresentMon console collector under `tools\presentmon\PresentMon.exe`; GamePulseMonitor prefers displayed-frame timing (`MsBetweenDisplayChange` / `DisplayedTime`) from its live CSV stream and falls back to present timing when display timing is unavailable.
