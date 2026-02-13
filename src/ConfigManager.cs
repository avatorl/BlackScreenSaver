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
                    Save(config); // persist migration
                }

                config.TargetScreenIndices ??= new List<int> { 1 };

                // Strip out the primary screen index — it must never be blacked out
                int primaryIdx = ScreenManager.GetPrimaryScreenIndex();
                config.TargetScreenIndices.Remove(primaryIdx);

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
