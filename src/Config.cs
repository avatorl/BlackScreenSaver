namespace BlackScreenSaver;

/// <summary>
/// Persisted description of a selected screen.
/// </summary>
public class PersistedScreenSelection
{
    /// <summary>
    /// Stable monitor interface name returned by Windows for this monitor.
    /// This is preferred over <see cref="DeviceName"/> because DISPLAY numbering can change.
    /// </summary>
    public string MonitorInterfaceName { get; set; } = string.Empty;

    /// <summary>
    /// GDI device name (for example, \\.\DISPLAY2) kept for backward compatibility.
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Last known screen bounds used as a fallback when Windows cannot provide a stable monitor identity.
    /// </summary>
    public ScreenBoundsSnapshot Bounds { get; set; } = new();
}

/// <summary>
/// Serializable snapshot of screen bounds.
/// </summary>
public class ScreenBoundsSnapshot
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public static ScreenBoundsSnapshot FromRectangle(Rectangle rectangle)
    {
        return new ScreenBoundsSnapshot
        {
            Left = rectangle.Left,
            Top = rectangle.Top,
            Width = rectangle.Width,
            Height = rectangle.Height
        };
    }

    public Rectangle ToRectangle()
    {
        return new Rectangle(Left, Top, Width, Height);
    }
}

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Indices of the target screens to black out (0-based).
    /// Derived at runtime from <see cref="TargetScreenDeviceNames"/>.
    /// </summary>
    public List<int> TargetScreenIndices { get; set; } = new() { 1 };

    /// <summary>
    /// Stable device names (e.g. \\.\DISPLAY3) for the target screens.
    /// This is kept for backward compatibility with older configs.
    /// </summary>
    public List<string> TargetScreenDeviceNames { get; set; } = new();

    /// <summary>
    /// Persisted selected screens with stable monitor identities and fallback position data.
    /// This is the preferred source of truth.
    /// </summary>
    public List<PersistedScreenSelection> TargetScreens { get; set; } = new();

    /// <summary>
    /// Legacy property kept for backward-compatible deserialization.
    /// If present in JSON it will be migrated into TargetScreenIndices.
    /// </summary>
    public int? TargetScreenIndex { get; set; }

    /// <summary>
    /// Number of seconds the cursor must be away from the target screens
    /// before the overlay appears.
    /// </summary>
    public int InactivityTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether the application should start with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;
}
