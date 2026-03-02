namespace BlackScreenSaver;

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
    /// This is the persisted source of truth — indices are resolved from these on load.
    /// </summary>
    public List<string> TargetScreenDeviceNames { get; set; } = new();

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

    /// <summary>
    /// Whether Ctrl+Alt+D should toggle Windows app mode between light and dark.
    /// </summary>
    public bool EnableDarkModeToggle { get; set; } = false;
}
