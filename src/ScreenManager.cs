using Microsoft.Win32;

namespace BlackScreenSaver;

/// <summary>
/// Provides helper methods for enumerating and describing screens.
/// </summary>
public static class ScreenManager
{
    /// <summary>
    /// Returns a user-friendly name for each screen, e.g. "Screen 1 (Primary) – 1920×1080".
    /// </summary>
    public static string GetScreenDisplayName(int index)
    {
        Screen[] screens = Screen.AllScreens;
        if (index < 0 || index >= screens.Length)
            return $"Screen {index + 1} (unavailable)";

        Screen s = screens[index];
        string primary = s.Primary ? " (Primary)" : "";
        return $"Screen {index + 1}{primary} – {s.Bounds.Width}×{s.Bounds.Height}";
    }

    /// <summary>
    /// Returns all screen display names.
    /// </summary>
    public static string[] GetAllScreenDisplayNames()
    {
        Screen[] screens = Screen.AllScreens;
        string[] names = new string[screens.Length];
        for (int i = 0; i < screens.Length; i++)
            names[i] = GetScreenDisplayName(i);
        return names;
    }

    /// <summary>
    /// Safely returns the screen at the given index, or the first screen if out of range.
    /// </summary>
    public static Screen GetScreen(int index)
    {
        Screen[] screens = Screen.AllScreens;
        if (index >= 0 && index < screens.Length)
            return screens[index];
        return screens[0];
    }

    /// <summary>
    /// Determines which screen the cursor is currently on (by index).
    /// Returns -1 if the cursor position doesn't match any screen.
    /// </summary>
    public static int GetCurrentCursorScreenIndex()
    {
        Point pos = Cursor.Position;
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i].Bounds.Contains(pos))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Returns the 0-based index of the primary screen (where the system tray lives).
    /// Returns 0 if no primary screen is found.
    /// </summary>
    public static int GetPrimaryScreenIndex()
    {
        Screen[] screens = Screen.AllScreens;
        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i].Primary)
                return i;
        }
        return 0;
    }

    /// <summary>
    /// Returns a list of screens for the given indices (skipping invalid ones).
    /// </summary>
    public static List<Screen> GetScreens(IEnumerable<int> indices)
    {
        Screen[] screens = Screen.AllScreens;
        var result = new List<Screen>();
        foreach (int i in indices)
        {
            if (i >= 0 && i < screens.Length)
                result.Add(screens[i]);
        }
        return result;
    }

    /// <summary>
    /// Sets or removes the "Start with Windows" registry entry.
    /// </summary>
    public static void SetStartWithWindows(bool enable)
    {
        const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "BlackScreenSaver";

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(keyName, writable: true);
            if (key == null) return;

            if (enable)
            {
                string exePath = Application.ExecutablePath;
                key.SetValue(valueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Silently fail on registry errors
        }
    }
}
