namespace BlackScreenSaver;

/// <summary>
/// Application configuration model.
/// </summary>
public class AppConfig
{
    /// <summary>
    /// Indices of the target screens to black out (0-based).
    /// Multiple screens can be selected.
    /// </summary>
    public List<int> TargetScreenIndices { get; set; } = new() { 1 };

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
