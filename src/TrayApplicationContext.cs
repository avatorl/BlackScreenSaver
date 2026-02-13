using Microsoft.Win32;

namespace BlackScreenSaver;

/// <summary>
/// The core application context that manages the tray icon, cursor monitor,
/// overlay window, and settings interaction.
/// </summary>
public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly OverlayWindow _overlay;
    private readonly CursorMonitor _monitor;
    private AppConfig _config;

    public TrayApplicationContext()
    {
        _config = ConfigManager.Load();

        // --- Overlay window ---
        _overlay = new OverlayWindow();

        // --- Cursor monitor ---
        _monitor = new CursorMonitor
        {
            TargetScreenIndex = _config.TargetScreenIndex,
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

    private void OnInactivityDetected(Screen targetScreen)
    {
        _overlay.ShowOnScreen(targetScreen);
    }

    private void OnActivityDetected()
    {
        _overlay.HideOverlay();
    }

    private void ToggleOverlay()
    {
        if (_overlay.Visible)
        {
            _overlay.HideOverlay();
        }
        else
        {
            Screen targetScreen = ScreenManager.GetScreen(_config.TargetScreenIndex);
            _overlay.ShowOnScreen(targetScreen);
        }
    }

    private void ShowSettings()
    {
        // Pause monitoring while settings are open
        _monitor.Stop();
        _overlay.HideOverlay();

        using var form = new SettingsForm(_config);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.ResultConfig;
            ConfigManager.Save(_config);
            ScreenManager.SetStartWithWindows(_config.StartWithWindows);

            // Apply new settings
            _monitor.TargetScreenIndex = _config.TargetScreenIndex;
            _monitor.InactivityTimeoutSeconds = _config.InactivityTimeoutSeconds;
        }

        _monitor.Start();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // If screens changed, validate the target index
        if (_config.TargetScreenIndex >= Screen.AllScreens.Length)
        {
            _config.TargetScreenIndex = 0;
            _monitor.TargetScreenIndex = 0;
            ConfigManager.Save(_config);
        }
    }

    private void ExitApplication()
    {
        _monitor.Stop();
        _monitor.Dispose();
        _overlay.HideOverlay();
        _overlay.Close();
        _overlay.Dispose();
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
