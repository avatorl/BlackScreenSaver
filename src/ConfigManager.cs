using System.Text.Json;

namespace BlackScreenSaver;

/// <summary>
/// Manages loading and saving application configuration to a JSON file.
/// </summary>
public static class ConfigManager
{
    private static readonly string ConfigFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BlackScreenSaver");

    private static readonly string ConfigFile = Path.Combine(ConfigFolder, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Loads configuration from disk, or returns defaults if the file doesn't exist.
    /// Resolves stable device names to current screen indices.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                string json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();

                // Migrate legacy single-index property
                if (config.TargetScreenIndex.HasValue)
                {
                    if (config.TargetScreenIndices == null || config.TargetScreenIndices.Count == 0)
                    {
                        config.TargetScreenIndices = new List<int> { config.TargetScreenIndex.Value };
                    }
                    config.TargetScreenIndex = null;
                }

                config.TargetScreenIndices ??= new List<int> { 1 };
                config.TargetScreenDeviceNames ??= new List<string>();

                bool needsSave = false;
                Screen[] screens = Screen.AllScreens;

                if (config.TargetScreenDeviceNames.Count > 0)
                {
                    // Resolve device names to current indices (stable across reboots)
                    config.TargetScreenIndices = ResolveDeviceNamesToIndices(config.TargetScreenDeviceNames, screens);
                }
                else if (config.TargetScreenIndices.Count > 0)
                {
                    // Legacy config without device names — migrate
                    config.TargetScreenDeviceNames = ResolveIndicesToDeviceNames(config.TargetScreenIndices, screens);
                    needsSave = true;
                }

                // Strip out the primary screen — it must never be blacked out
                int primaryIdx = ScreenManager.GetPrimaryScreenIndex();
                string? primaryDeviceName = primaryIdx >= 0 && primaryIdx < screens.Length
                    ? screens[primaryIdx].DeviceName : null;

                config.TargetScreenIndices.Remove(primaryIdx);
                if (primaryDeviceName != null)
                    config.TargetScreenDeviceNames.Remove(primaryDeviceName);

                // If this leaves no target screens (for example, single-monitor setups),
                // fall back to the primary screen so the app remains functional.
                if (config.TargetScreenIndices.Count == 0)
                {
                    config.TargetScreenIndices.Add(primaryIdx);
                }

                if (needsSave)
                    Save(config);

                return config;
            }
        }
        catch
        {
            // If reading fails, return defaults
        }

        return new AppConfig();
    }

    /// <summary>
    /// Resolves a list of device names to current screen indices.
    /// Unrecognized device names are silently skipped.
    /// </summary>
    public static List<int> ResolveDeviceNamesToIndices(List<string> deviceNames, Screen[] screens)
    {
        var indices = new List<int>();
        foreach (string name in deviceNames)
        {
            for (int i = 0; i < screens.Length; i++)
            {
                if (string.Equals(screens[i].DeviceName, name, StringComparison.OrdinalIgnoreCase))
                {
                    indices.Add(i);
                    break;
                }
            }
        }
        return indices;
    }

    /// <summary>
    /// Resolves a list of screen indices to their device names.
    /// Out-of-range indices are silently skipped.
    /// </summary>
    public static List<string> ResolveIndicesToDeviceNames(List<int> indices, Screen[] screens)
    {
        var names = new List<string>();
        foreach (int idx in indices)
        {
            if (idx >= 0 && idx < screens.Length)
                names.Add(screens[idx].DeviceName);
        }
        return names;
    }

    /// <summary>
    /// Saves configuration to disk.
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(ConfigFolder);
            string json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigFile, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
