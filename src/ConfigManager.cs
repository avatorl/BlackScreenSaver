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
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
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
