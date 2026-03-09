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

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

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

    // --- CCD (Connecting and Configuring Displays) P/Invoke ---

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    private const uint QDC_ONLY_ACTIVE_PATHS = 2;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    /// <summary>
    /// Returns true if the output technology represents an internal/embedded display
    /// (laptop panel, tablet screen, etc.).
    /// </summary>
    private static bool IsInternalOutputTechnology(uint outputTechnology)
    {
        return outputTechnology switch
        {
            6 => true,            // LVDS
            11 => true,           // DisplayPort Embedded (eDP)
            13 => true,           // UDI Embedded
            0x80000000 => true,   // Internal (generic)
            _ => false
        };
    }

    /// <summary>
    /// Builds a map from GDI device name (e.g. \\.\DISPLAY1) to the Windows Display Settings
    /// number, using the CCD QueryDisplayConfig API. Returns null if the query fails.
    ///
    /// Windows numbers internal (laptop) displays first, then external displays,
    /// each group sorted by connector instance.
    /// </summary>
    private static Dictionary<string, int>? BuildDisplaySettingsNumberMap()
    {
        try
        {
            int rc = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out uint numPaths, out uint numModes);
            if (rc != 0) return null;

            var paths = new DISPLAYCONFIG_PATH_INFO[numPaths];
            var modes = new DISPLAYCONFIG_MODE_INFO[numModes];
            rc = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref numPaths, paths, ref numModes, modes, IntPtr.Zero);
            if (rc != 0) return null;

            var entries = new List<(string gdiName, bool isInternal, uint connectorInstance)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (uint i = 0; i < numPaths; i++)
            {
                var path = paths[i];

                var srcName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                srcName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                srcName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
                srcName.header.adapterId = path.sourceInfo.adapterId;
                srcName.header.id = path.sourceInfo.id;
                if (DisplayConfigGetDeviceInfo(ref srcName) != 0) continue;

                string gdiName = srcName.viewGdiDeviceName?.Trim().TrimEnd('\0') ?? "";
                if (string.IsNullOrEmpty(gdiName) || !seen.Add(gdiName)) continue;

                var tgtName = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                tgtName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                tgtName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
                tgtName.header.adapterId = path.targetInfo.adapterId;
                tgtName.header.id = path.targetInfo.id;
                DisplayConfigGetDeviceInfo(ref tgtName);

                entries.Add((gdiName, IsInternalOutputTechnology(path.targetInfo.outputTechnology), tgtName.connectorInstance));
            }

            // Sort: internal first, then external; within each group by connector instance
            entries.Sort((a, b) =>
            {
                int cmp = b.isInternal.CompareTo(a.isInternal); // true (internal) before false
                if (cmp != 0) return cmp;
                return a.connectorInstance.CompareTo(b.connectorInstance);
            });

            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
                map[entries[i].gdiName] = i + 1;

            return map;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, int>? _displayNumberCache;

    /// <summary>
    /// Invalidates the cached display-number map so it is rebuilt on the next call.
    /// Should be called when display settings change.
    /// </summary>
    public static void InvalidateDisplayNumberCache()
    {
        _displayNumberCache = null;
    }

    /// <summary>
    /// Returns the Windows Display Settings number for a screen.
    /// This matches the "Identify" numbering in System &gt; Display.
    /// Falls back to the DISPLAY suffix number if CCD query fails.
    /// </summary>
    public static int GetWindowsDisplayNumber(Screen screen, int fallbackIndex)
    {
        _displayNumberCache ??= BuildDisplaySettingsNumberMap();

        if (_displayNumberCache != null &&
            _displayNumberCache.TryGetValue(screen.DeviceName, out int number))
            return number;

        // Fallback: extract trailing digits from DeviceName
        string name = screen.DeviceName;
        int i = name.Length - 1;
        while (i >= 0 && char.IsDigit(name[i]))
            i--;
        if (i < name.Length - 1 && int.TryParse(name.AsSpan(i + 1), out int parsed))
            return parsed;
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

    /// <summary>
    /// Returns the 0-based screen index that contains the center of the current
    /// foreground window, or -1 if there is no meaningful foreground window
    /// (e.g. desktop, shell, or no window).
    /// </summary>
    public static int GetForegroundWindowScreenIndex()
    {
        try
        {
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow == IntPtr.Zero)
                return -1;

            // Ignore desktop / shell windows — they don't represent real app focus.
            if (fgWindow == GetDesktopWindow() || fgWindow == GetShellWindow())
                return -1;

            if (!GetWindowRect(fgWindow, out RECT rect))
                return -1;

            // Use the center of the window to decide which screen it belongs to.
            int cx = (rect.Left + rect.Right) / 2;
            int cy = (rect.Top + rect.Bottom) / 2;
            Point center = new(cx, cy);

            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i].Bounds.Contains(center))
                    return i;
            }
        }
        catch
        {
            // Silently fail — treat as "no focused screen".
        }

        return -1;
    }
}
