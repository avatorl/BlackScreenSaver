using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace BlackScreenSaver;

/// <summary>
/// Runtime description of a currently attached screen and its hardware-backed monitor identity.
/// </summary>
public sealed class CurrentScreenIdentity
{
    public int Index { get; init; }
    public Screen Screen { get; init; } = null!;
    public string DeviceName { get; init; } = string.Empty;
    public string MonitorInterfaceName { get; init; } = string.Empty;
    public ScreenBoundsSnapshot Bounds { get; init; } = new();
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Provides helper methods for enumerating and describing screens.
/// </summary>
public static class ScreenManager
{
    private const int EddGetDeviceInterfaceName = 0x00000001;
    private const int DisplayDeviceAttachedToDesktop = 0x00000001;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool EnumDisplayDevices(
        string? lpDevice,
        uint iDevNum,
        ref DISPLAY_DEVICE lpDisplayDevice,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    /// <summary>
    /// Extracts the Windows display number from Screen.DeviceName (e.g. "\\.\DISPLAY3" → 3).
    /// Falls back to array index + 1 if parsing fails.
    /// </summary>
    public static int GetWindowsDisplayNumber(Screen screen, int fallbackIndex)
    {
        string name = screen.DeviceName;
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i]))
            i--;
        if (i < name.Length - 1 && int.TryParse(name.AsSpan(i + 1), out int number))
            return number;
        return fallbackIndex + 1;
    }

    /// <summary>
    /// Returns a user-friendly name for each screen, e.g. "Screen 1 (Primary) – 1920×1080".
    /// The number matches the Windows Display Settings numbering.
    /// </summary>
    public static string GetScreenDisplayName(int index)
    {
        Screen[] screens = Screen.AllScreens;
        if (index < 0 || index >= screens.Length)
            return $"Screen {index + 1} (unavailable)";

        Screen s = screens[index];
        int displayNum = GetWindowsDisplayNumber(s, index);
        string primary = s.Primary ? " (Primary)" : "";
        return $"Screen {displayNum}{primary} – {s.Bounds.Width}×{s.Bounds.Height}";
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

    /// <summary>
    /// Returns runtime screen identities for all attached screens.
    /// </summary>
    public static List<CurrentScreenIdentity> GetCurrentScreenIdentities()
    {
        Screen[] screens = Screen.AllScreens;
        var result = new List<CurrentScreenIdentity>(screens.Length);

        for (int i = 0; i < screens.Length; i++)
        {
            Screen screen = screens[i];
            result.Add(new CurrentScreenIdentity
            {
                Index = i,
                Screen = screen,
                DeviceName = screen.DeviceName,
                MonitorInterfaceName = GetMonitorInterfaceName(screen.DeviceName),
                Bounds = ScreenBoundsSnapshot.FromRectangle(screen.Bounds),
                IsPrimary = screen.Primary
            });
        }

        return result;
    }

    /// <summary>
    /// Creates persisted screen selections for the supplied indices.
    /// </summary>
    public static List<PersistedScreenSelection> CreatePersistedSelections(IEnumerable<int> indices)
    {
        var identities = GetCurrentScreenIdentities();
        var selections = new List<PersistedScreenSelection>();

        foreach (int index in indices)
        {
            CurrentScreenIdentity? identity = identities.FirstOrDefault(s => s.Index == index);
            if (identity == null)
                continue;

            selections.Add(new PersistedScreenSelection
            {
                MonitorInterfaceName = identity.MonitorInterfaceName,
                DeviceName = identity.DeviceName,
                Bounds = identity.Bounds
            });
        }

        return selections;
    }

    private static string GetMonitorInterfaceName(string deviceName)
    {
        for (uint monitorIndex = 0; ; monitorIndex++)
        {
            var monitorDevice = CreateDisplayDevice();
            if (!EnumDisplayDevices(deviceName, monitorIndex, ref monitorDevice, EddGetDeviceInterfaceName))
                break;

            bool attachedToDesktop = (monitorDevice.StateFlags & DisplayDeviceAttachedToDesktop) != 0;
            string interfaceName = NormalizeMonitorInterfaceName(monitorDevice.DeviceID);
            if (!string.IsNullOrEmpty(interfaceName) && attachedToDesktop)
                return interfaceName;

            if (!string.IsNullOrEmpty(interfaceName))
                return interfaceName;
        }

        return string.Empty;
    }

    private static DISPLAY_DEVICE CreateDisplayDevice()
    {
        return new DISPLAY_DEVICE
        {
            cb = Marshal.SizeOf<DISPLAY_DEVICE>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceID = string.Empty,
            DeviceKey = string.Empty
        };
    }

    private static string NormalizeMonitorInterfaceName(string value)
    {
        return value.Trim().TrimEnd('\0').ToUpperInvariant();
    }
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
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to update 'Start with Windows': {ex.Message}",
                "Black Screen Saver",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Checks if the given screen is currently occupied by a fullscreen window (e.g., video, game, slideshow).
    /// Returns true if a fullscreen app is detected on this screen.
    /// </summary>
    public static bool IsScreenFullscreenOccupied(int screenIndex)
    {
        try
        {
            Screen[] screens = Screen.AllScreens;
            if (screenIndex < 0 || screenIndex >= screens.Length)
                return false;

            Screen screen = screens[screenIndex];
            IntPtr fgWindow = GetForegroundWindow();

            if (fgWindow == IntPtr.Zero)
                return false;

            if (GetWindowRect(fgWindow, out RECT rect))
            {
                // Check if the window bounds match (or nearly match) the screen bounds
                // allowing a small tolerance for window decorations
                Rectangle screenBounds = screen.Bounds;
                int tolerance = 5; // pixels

                bool matchesWidth = Math.Abs(rect.Right - rect.Left - screenBounds.Width) <= tolerance;
                bool matchesHeight = Math.Abs(rect.Bottom - rect.Top - screenBounds.Height) <= tolerance;
                bool matchesPosition = Math.Abs(rect.Left - screenBounds.Left) <= tolerance &&
                                      Math.Abs(rect.Top - screenBounds.Top) <= tolerance;

                if (matchesWidth && matchesHeight && matchesPosition)
                    return true;
            }
        }
        catch
        {
            // Silently fail on fullscreen detection errors
        }

        return false;
    }
}
