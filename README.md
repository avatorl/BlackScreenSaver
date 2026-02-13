# Black Screen Saver

A lightweight Windows 11 system-tray application that blacks out a selected monitor when the cursor is inactive on it.

## Description

Not a regular Windows screensaver — this is a custom tray app designed for multi-monitor setups. It monitors the cursor position and, after a configurable timeout, displays a full-screen black overlay on the selected monitor. The moment the cursor enters that screen, the overlay disappears instantly. Keyboard and mouse activity on other screens is unaffected.

## Features

- **System tray** — runs silently in the notification area
- **Multi-monitor aware** — detects all connected screens and lets you pick which one to black out
- **Configurable timeout** — set how many seconds (1–300) before the screen goes black
- **Instant on/off** — overlay appears/disappears with no fade or animation
- **Manual toggle** — right-click tray icon → "Black Out Now" to toggle immediately
- **Start with Windows** — optional auto-start via registry
- **Single instance** — prevents duplicate processes
- **Handles display changes** — adapts when monitors are connected/disconnected

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or build as self-contained)

## Build

```bash
cd blackscreensaver
dotnet build
```

To publish a self-contained single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Usage

1. Run `BlackScreenSaver.exe` — a tray icon (black square) appears in the notification area
2. **Right-click** the tray icon:
   - **Settings...** — choose target screen, set timeout, toggle auto-start
   - **Black Out Now** — manually toggle the overlay
   - **Exit** — close the application
3. **Double-click** the tray icon to open Settings
4. Settings are saved to `%APPDATA%\BlackScreenSaver\config.json`

## Project Structure

```
blackscreensaver/
├── BlackScreenSaver.csproj
├── Resources/
│   └── icon.ico
└── src/
    ├── Program.cs              # Entry point, single-instance mutex
    ├── TrayApplicationContext.cs # Tray icon, menus, app lifecycle
    ├── CursorMonitor.cs         # Timer-based cursor position tracking
    ├── OverlayWindow.cs         # Borderless topmost black window
    ├── ScreenManager.cs         # Multi-monitor helpers, registry
    ├── SettingsForm.cs          # Configuration dialog
    ├── Config.cs                # Settings model
    └── ConfigManager.cs         # JSON persistence
```

## License

MIT
