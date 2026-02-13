namespace BlackScreenSaver;

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Index of the target screen to black out (0-based).
    /// </summary>
    public int TargetScreenIndex { get; set; } = 1;

    /// <summary>
    /// Number of seconds the cursor must be away from the target screen
    /// before the overlay appears.
    /// </summary>
    public int InactivityTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether the application should start with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;
}
