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

    public TrayApplicationContext()
    {
        _config = ConfigManager.Load();

        // --- Cursor monitor ---
        _monitor = new CursorMonitor
        {
            TargetScreenIndices = new HashSet<int>(_config.TargetScreenIndices),
            InactivityTimeoutSeconds = _config.InactivityTimeoutSeconds
        };
        _monitor.InactivityDetected += OnInactivityDetected;
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

    private void OnInactivityDetected(IReadOnlyList<Screen> targetScreens)
    {
        ShowOverlays(targetScreens);
    }

    private void OnActivityDetected(int screenIndex)
    {
        HideOverlayForScreen(screenIndex);
    }

    /// <summary>
    /// Maps screen index → overlay window for active overlays.
    /// </summary>
    private readonly Dictionary<int, OverlayWindow> _screenOverlayMap = new();

    private void ShowOverlays(IReadOnlyList<Screen> screens)
    {
        // Ensure we have enough overlay windows
        while (_overlays.Count < screens.Count)
            _overlays.Add(new OverlayWindow());

        _screenOverlayMap.Clear();
        for (int i = 0; i < screens.Count; i++)
        {
            _overlays[i].ShowOnScreen(screens[i]);

            // Find the screen index for this Screen object
            int idx = Array.IndexOf(Screen.AllScreens, screens[i]);
            if (idx >= 0)
                _screenOverlayMap[idx] = _overlays[i];
        }
    }

    private void HideOverlayForScreen(int screenIndex)
    {
        if (_screenOverlayMap.TryGetValue(screenIndex, out var overlay))
        {
            overlay.HideOverlay();
            _screenOverlayMap.Remove(screenIndex);
        }
    }

    private void HideAllOverlays()
    {
        foreach (var overlay in _overlays)
            overlay.HideOverlay();
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
        }

        _monitor.Start();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Remove any indices that are now out of range or point at the primary screen
        int screenCount = Screen.AllScreens.Length;
        int primaryIdx = ScreenManager.GetPrimaryScreenIndex();
        var validIndices = _config.TargetScreenIndices.Where(i => i < screenCount && i != primaryIdx).ToList();
        if (validIndices.Count != _config.TargetScreenIndices.Count)
        {
            _config.TargetScreenIndices = validIndices.Count > 0 ? validIndices : new List<int> { 0 };
            _monitor.TargetScreenIndices = new HashSet<int>(_config.TargetScreenIndices);
            ConfigManager.Save(_config);
        }
    }

    private void ExitApplication()
    {
        _monitor.Stop();
        _monitor.Dispose();
        HideAllOverlays();
        foreach (var overlay in _overlays)
        {
            overlay.Close();
            overlay.Dispose();
        }
        _overlays.Clear();
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
                    return new Icon(stream);
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
        return Icon.FromHandle(hIcon);
    }
}
