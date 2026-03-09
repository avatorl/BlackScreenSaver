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
                config.TargetScreens ??= new List<PersistedScreenSelection>();

                bool needsSave = false;
                List<CurrentScreenIdentity> currentScreens = ScreenManager.GetCurrentScreenIdentities();
                Screen[] screens = currentScreens.Select(s => s.Screen).ToArray();

                if (config.TargetScreens.Count > 0)
                {
                    config.TargetScreenIndices = ResolveTargetScreensToIndices(config.TargetScreens, currentScreens);
                }
                else if (config.TargetScreenDeviceNames.Count > 0)
                {
                    // Migrate legacy device-name configs to the richer persisted model.
                    config.TargetScreenIndices = ResolveDeviceNamesToIndices(config.TargetScreenDeviceNames, screens);
                    config.TargetScreens = BuildSelectionsFromDeviceNames(config.TargetScreenDeviceNames, currentScreens);
                    needsSave = config.TargetScreens.Count > 0;
                }
                else if (config.TargetScreenIndices.Count > 0)
                {
                    // Legacy config without device names — migrate
                    config.TargetScreenDeviceNames = ResolveIndicesToDeviceNames(config.TargetScreenIndices, screens);
                    config.TargetScreens = ScreenManager.CreatePersistedSelections(config.TargetScreenIndices);
                    needsSave = true;
                }

                // Strip out the primary screen — it must never be blacked out
                int primaryIdx = ScreenManager.GetPrimaryScreenIndex();
                string? primaryDeviceName = primaryIdx >= 0 && primaryIdx < screens.Length
                    ? screens[primaryIdx].DeviceName : null;
                string? primaryMonitorInterfaceName = primaryIdx >= 0 && primaryIdx < currentScreens.Count
                    ? currentScreens[primaryIdx].MonitorInterfaceName : null;

                config.TargetScreenIndices.Remove(primaryIdx);
                if (primaryDeviceName != null)
                    config.TargetScreenDeviceNames.Remove(primaryDeviceName);
                config.TargetScreens.RemoveAll(selection =>
                    string.Equals(selection.DeviceName, primaryDeviceName, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(primaryMonitorInterfaceName)
                        && string.Equals(selection.MonitorInterfaceName, primaryMonitorInterfaceName, StringComparison.OrdinalIgnoreCase)));

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
    /// Resolves persisted screen selections to the currently attached screen indices.
    /// Matching order is: stable monitor interface name, legacy device name, then bounds fallback.
    /// </summary>
    public static List<int> ResolveTargetScreensToIndices(
        IReadOnlyList<PersistedScreenSelection> targetScreens,
        IReadOnlyList<CurrentScreenIdentity> currentScreens)
    {
        var resolvedIndices = new List<int>();
        var usedIndices = new HashSet<int>();

        foreach (PersistedScreenSelection target in targetScreens)
        {
            int index = FindUnmatchedScreenIndex(
                currentScreens,
                usedIndices,
                screen => !string.IsNullOrWhiteSpace(target.MonitorInterfaceName)
                    && string.Equals(screen.MonitorInterfaceName, target.MonitorInterfaceName, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                index = FindUnmatchedScreenIndex(
                    currentScreens,
                    usedIndices,
                    screen => !string.IsNullOrWhiteSpace(target.DeviceName)
                        && string.Equals(screen.DeviceName, target.DeviceName, StringComparison.OrdinalIgnoreCase));
            }

            if (index < 0)
            {
                index = FindBestBoundsMatch(target.Bounds, currentScreens, usedIndices);
            }

            if (index >= 0)
            {
                resolvedIndices.Add(index);
                usedIndices.Add(index);
            }
        }

        return resolvedIndices;
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

    private static List<PersistedScreenSelection> BuildSelectionsFromDeviceNames(
        IReadOnlyList<string> deviceNames,
        IReadOnlyList<CurrentScreenIdentity> currentScreens)
    {
        var selections = new List<PersistedScreenSelection>();

        foreach (string deviceName in deviceNames)
        {
            CurrentScreenIdentity? match = currentScreens.FirstOrDefault(screen =>
                string.Equals(screen.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                selections.Add(new PersistedScreenSelection
                {
                    MonitorInterfaceName = match.MonitorInterfaceName,
                    DeviceName = match.DeviceName,
                    Bounds = match.Bounds
                });
                continue;
            }

            selections.Add(new PersistedScreenSelection
            {
                DeviceName = deviceName,
                Bounds = new ScreenBoundsSnapshot()
            });
        }

        return selections;
    }

    private static int FindUnmatchedScreenIndex(
        IReadOnlyList<CurrentScreenIdentity> currentScreens,
        ISet<int> usedIndices,
        Func<CurrentScreenIdentity, bool> predicate)
    {
        foreach (CurrentScreenIdentity screen in currentScreens)
        {
            if (usedIndices.Contains(screen.Index))
                continue;

            if (predicate(screen))
                return screen.Index;
        }

        return -1;
    }

    private static int FindBestBoundsMatch(
        ScreenBoundsSnapshot? targetBounds,
        IReadOnlyList<CurrentScreenIdentity> currentScreens,
        ISet<int> usedIndices)
    {
        if (targetBounds == null)
            return -1;

        int bestIndex = -1;
        long bestScore = long.MaxValue;

        foreach (CurrentScreenIdentity screen in currentScreens)
        {
            if (usedIndices.Contains(screen.Index))
                continue;

            long score = GetBoundsScore(targetBounds, screen.Bounds);
            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = screen.Index;
            }
        }

        return bestIndex;
    }

    private static long GetBoundsScore(ScreenBoundsSnapshot left, ScreenBoundsSnapshot right)
    {
        long leftCenterX = left.Left + (left.Width / 2L);
        long leftCenterY = left.Top + (left.Height / 2L);
        long rightCenterX = right.Left + (right.Width / 2L);
        long rightCenterY = right.Top + (right.Height / 2L);

        long positionDelta = Math.Abs(leftCenterX - rightCenterX) + Math.Abs(leftCenterY - rightCenterY);
        long sizeDelta = Math.Abs(left.Width - right.Width) + Math.Abs(left.Height - right.Height);

        return (positionDelta * 10) + sizeDelta;
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
