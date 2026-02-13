# Plan: Windows 11 Multi-Monitor Black Screen Tray App

A lightweight C# Windows Forms tray application that monitors cursor inactivity on a user-selected screen and displays an instant black overlay until the cursor returns. Configuration UI allows screen selection and timeout adjustment.

## Key Decisions:

C# Windows Forms for native Windows integration and minimal overhead
Timer-based cursor polling (every 300ms) rather than system hooks for simplicity
Per-screen inactivity tracking with user-selectable target monitor
Configurable timeout stored in JSON file
Instant overlay appearance/disappearance (no animations)
Steps

## Initialize Project Structure

Create .NET 6+ Windows Forms solution in blackscreensaver
Add project files: BlackScreenSaver.csproj, BlackScreenSaver.sln
Set output type to WinExe (no console window)
Create subdirectories: src/, Resources/
Implement Configuration System

Create Config.cs model class with properties: TargetScreenIndex, InactivityTimeoutSeconds, StartWithWindows
Create ConfigManager.cs to handle JSON serialization/deserialization to %APPDATA%\BlackScreenSaver\config.json
Default timeout: 10 seconds
Default screen: index 1 (first secondary monitor if exists, otherwise 0)
Build System Tray Application

Create TrayApplicationContext.cs extending ApplicationContext
Implement NotifyIcon with icon resource (white/black screen icon)
Add ContextMenuStrip with menu items: "Settings", "Exit"
Ensure single instance using Mutex in Program.cs
Implement Screen Management

Create ScreenManager.cs wrapper around Screen.AllScreens
Method GetScreenByIndex(int) with bounds validation
Method GetScreenFriendlyName(int) returning "Primary Monitor (1920x1080)" format
Subscribe to SystemEvents.DisplaySettingsChanged to refresh screen list
Create Settings UI

Create SettingsForm.cs with:
ComboBox to select target screen (populated from ScreenManager)
NumericUpDown for timeout (range: 1-300 seconds)
CheckBox for "Start with Windows" (modify registry HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run)
Save/Cancel buttons updating ConfigManager
Implement Cursor Monitoring

Create CursorMonitor.cs class
Use System.Windows.Forms.Timer (300ms interval) polling Cursor.Position
Track: lastSeenOnTargetScreen timestamp
Logic: If cursor on target screen → reset timer; If cursor away for >timeout → trigger overlay event
Event: InactivityDetected(Screen targetScreen), ActivityDetected()
Build Black Overlay Window

Create OverlayWindow.cs extending Form
Set properties: FormBorderStyle = None, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false, Opacity = 1.0
Method ShowOnScreen(Screen) positioning form to screen bounds
Override OnMouseMove to detect cursor entry → call Hide() immediately
Alternative: Use transparent cursor hit-test, rely on CursorMonitor for dismissal
Wire Application Logic

In TrayApplicationContext.cs:
Initialize ConfigManager, ScreenManager, CursorMonitor, OverlayWindow
Subscribe to CursorMonitor.InactivityDetected → show overlay on target screen
Subscribe to CursorMonitor.ActivityDetected → hide overlay
Handle "Settings" menu click → show SettingsForm, reload config on save
Add Application Entry Point

Update Program.cs:
Check for existing instance using Mutex
Call Application.EnableVisualStyles(), Application.SetCompatibleTextRenderingDefault(false)
Run Application.Run(new TrayApplicationContext())
Update Documentation

Update README.md with:
Description of functionality
Build instructions (dotnet build, dotnet publish)
Usage: right-click tray icon → Settings
Requirements: .NET 6.0+ Runtime or self-contained build
Verification

Build project: dotnet build (no errors)
Run executable, verify tray icon appears
Open Settings, select specific screen, set 5sec timeout, save
Move cursor away from selected screen → black overlay appears after 5 seconds
Move cursor back to blacked screen → overlay disappears instantly
Test with monitor disconnect/reconnect scenarios
Test "Start with Windows" registry entry creation
Technical Notes

Cursor dismissal: CursorMonitor detects cursor re-entry to screen bounds and triggers ActivityDetected event
Thread safety: All UI operations on main thread via Invoke if needed
Performance target: <5MB RAM, negligible CPU usage