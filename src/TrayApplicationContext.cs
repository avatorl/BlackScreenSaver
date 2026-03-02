using Microsoft.Win32;

namespace BlackScreenSaver;

/// <summary>
/// The core application context that manages the tray icon, cursor monitor,
/// overlay windows, and settings interaction.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly List<OverlayWindow> _overlays = new();
    private readonly CursorMonitor _monitor;
    private AppConfig _config;
    private HotkeyMessageWindow? _hotkeyWindow;

    private const int WmHotkey = 0x0312;
    private const int HotkeyIdDarkMode = 1;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModWin = 0x0008;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Minimal NativeWindow that forwards WM_HOTKEY to an action.
    /// </summary>
    private sealed class HotkeyMessageWindow : NativeWindow
    {
        public Action? HotkeyPressed;

        public HotkeyMessageWindow()
        {
            CreateHandle(new System.Windows.Forms.CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
                HotkeyPressed?.Invoke();
            else
                base.WndProc(ref m);
        }
    }

    public TrayApplicationContext()
    {
        _config = ConfigManager.Load();

        // --- Cursor monitor ---
        _monitor = new CursorMonitor
        {
            TargetScreenIndices = new HashSet<int>(_config.TargetScreenIndices),
            InactivityTimeoutSeconds = _config.InactivityTimeoutSeconds
        };
        _monitor.ScreenInactivityDetected += OnScreenInactivityDetected;
        _monitor.ActivityDetected += OnActivityDetected;

        // --- Tray icon ---
        _trayIcon = new NotifyIcon
        {
            Icon = LoadEmbeddedIcon(),
            Text = "Black Screen Saver",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        // Listen for display changes (monitor plug/unplug)
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // Start monitoring
        _monitor.Start();

        // Register Ctrl+Alt+D hotkey if enabled
        RegisterDarkModeHotkey();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => ShowSettings();

        var toggleItem = new ToolStripMenuItem("Black Out Now");
        toggleItem.Click += (_, _) => ToggleOverlay();

        var separator = new ToolStripSeparator();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.AddRange(new ToolStripItem[] { settingsItem, toggleItem, separator, exitItem });
        return menu;
    }

    private void OnScreenInactivityDetected(int screenIndex)
    {
        ShowOverlayForScreen(screenIndex);
    }

    private void OnActivityDetected(int screenIndex)
    {
        HideOverlayForScreen(screenIndex);
    }

    /// <summary>
    /// Maps screen index → overlay window for active overlays.
    /// </summary>
    private readonly Dictionary<int, OverlayWindow> _screenOverlayMap = new();

    /// <summary>
    /// Shows an overlay on a single screen by index.
    /// Skips if a fullscreen app (video, game, slideshow) is detected on the screen.
    /// </summary>
    private void ShowOverlayForScreen(int screenIndex)
    {
        if (_screenOverlayMap.ContainsKey(screenIndex))
            return; // already showing

        // Don't overlay if a fullscreen app is running on this screen
        if (ScreenManager.IsScreenFullscreenOccupied(screenIndex))
            return;

        Screen? screen = ScreenManager.GetScreen(screenIndex);
        if (screen == null) return;

        var overlay = new OverlayWindow();
        overlay.ShowOnScreen(screen);
        _overlays.Add(overlay);
        _screenOverlayMap[screenIndex] = overlay;

        // Ensure the cursor monitor knows an overlay is active so it will
        // fire ActivityDetected when the cursor enters this screen.
        // (Required for the "Black Out Now" path which bypasses the
        //  inactivity timeout and therefore never sets this flag itself.)
        _monitor.MarkOverlayActive(screenIndex);
    }

    /// <summary>
    /// Shows overlays on all given screens (used by "Black Out Now").
    /// </summary>
    private void ShowOverlays(IReadOnlyList<Screen> screens)
    {
        foreach (Screen s in screens)
        {
            int idx = Array.IndexOf(Screen.AllScreens, s);
            if (idx >= 0)
                ShowOverlayForScreen(idx);
        }
    }

    private void HideOverlayForScreen(int screenIndex)
    {
        if (_screenOverlayMap.TryGetValue(screenIndex, out var overlay))
        {
            overlay.HideOverlay();
            overlay.Close();
            overlay.Dispose();
            _overlays.Remove(overlay);
            _screenOverlayMap.Remove(screenIndex);
        }
    }

    private void HideAllOverlays()
    {
        foreach (var overlay in _overlays)
        {
            overlay.HideOverlay();
            overlay.Close();
            overlay.Dispose();
        }
        _overlays.Clear();
        _screenOverlayMap.Clear();
    }

    private bool AnyOverlayVisible()
    {
        return _overlays.Any(o => o.Visible);
    }

    private void ToggleOverlay()
    {
        if (AnyOverlayVisible())
        {
            HideAllOverlays();
        }
        else
        {
            List<Screen> targetScreens = ScreenManager.GetScreens(_config.TargetScreenIndices);
            ShowOverlays(targetScreens);
        }
    }

    private void ShowSettings()
    {
        // Pause monitoring while settings are open
        _monitor.Stop();
        HideAllOverlays();

        using var form = new SettingsForm(_config);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.ResultConfig;
            ConfigManager.Save(_config);
            ScreenManager.SetStartWithWindows(_config.StartWithWindows);

            // Apply new settings
            _monitor.TargetScreenIndices = new HashSet<int>(_config.TargetScreenIndices);
            _monitor.InactivityTimeoutSeconds = _config.InactivityTimeoutSeconds;

            // Re-apply hotkey registration (may have been toggled)
            RegisterDarkModeHotkey();
        }

        _monitor.Start();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Screen[] screens = Screen.AllScreens;
        int primaryIdx = ScreenManager.GetPrimaryScreenIndex();

        // Re-resolve device names to current indices (screen order may have changed).
        // Only update in-memory indices — do NOT mutate TargetScreenDeviceNames or save,
        // because the user's preference (which screens to black out) has not changed.
        List<int> resolvedIndices;
        if (_config.TargetScreenDeviceNames.Count > 0)
        {
            resolvedIndices = ConfigManager.ResolveDeviceNamesToIndices(
                _config.TargetScreenDeviceNames, screens);
        }
        else
        {
            resolvedIndices = new List<int>(_config.TargetScreenIndices);
        }

        // Exclude primary screen at runtime
        resolvedIndices.Remove(primaryIdx);

        if (resolvedIndices.Count == 0)
            resolvedIndices.Add(primaryIdx);

        _config.TargetScreenIndices = resolvedIndices;
        _monitor.TargetScreenIndices = new HashSet<int>(resolvedIndices);
    }

    private void RegisterDarkModeHotkey()
    {
        // Always unregister first to avoid duplicate registration
        if (_hotkeyWindow != null)
        {
            UnregisterHotKey(_hotkeyWindow.Handle, HotkeyIdDarkMode);
            _hotkeyWindow.DestroyHandle();
            _hotkeyWindow = null;
        }

        if (_config.EnableDarkModeToggle)
        {
            _hotkeyWindow = new HotkeyMessageWindow { HotkeyPressed = ToggleWindowsAppMode };
            RegisterHotKey(_hotkeyWindow.Handle, HotkeyIdDarkMode, ModControl | ModAlt | ModWin, (uint)Keys.D);
        }
    }

    private static void ToggleWindowsAppMode()
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        using var key = Registry.CurrentUser.OpenSubKey(keyPath, writable: true);
        if (key == null) return;
        int current = (int)(key.GetValue("AppsUseLightTheme") ?? 1);
        key.SetValue("AppsUseLightTheme", current == 0 ? 1 : 0, RegistryValueKind.DWord);
    }

    private void ExitApplication()
    {
        // Unregister hotkey before exit
        if (_hotkeyWindow != null)
        {
            UnregisterHotKey(_hotkeyWindow.Handle, HotkeyIdDarkMode);
            _hotkeyWindow.DestroyHandle();
            _hotkeyWindow = null;
        }

        _monitor.Stop();
        _monitor.Dispose();
        HideAllOverlays();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        Application.Exit();
    }

    /// <summary>
    /// Loads the embedded icon resource or falls back to a generated icon.
    /// </summary>
    private static Icon LoadEmbeddedIcon()
    {
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                using Stream? stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    // Request the exact size the system tray needs (typically 16x16)
                    // so Windows doesn't have to rescale on-the-fly, which caused
                    // flickering/blinking in the tray overflow area (issue #5).
                    var traySize = SystemInformation.SmallIconSize;
                    return new Icon(stream, traySize);
                }
            }
        }
        catch
        {
            // Fall through to generated icon
        }

        return GenerateIcon();
    }

    /// <summary>
    /// Generates a simple 16x16 icon: black square with white border.
    /// Used as a fallback when no .ico resource is embedded.
    /// </summary>
    private static Icon GenerateIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            g.DrawRectangle(Pens.DarkGray, 0, 0, 15, 15);
        }
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            // Clone the icon so it owns its own copy of the data,
            // independent of the unmanaged handle.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
