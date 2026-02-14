namespace BlackScreenSaver;

/// <summary>
/// Monitors the cursor position at regular intervals and raises events
/// when the cursor has been away from each target screen for the configured timeout,
/// or when the cursor returns to a target screen.
/// Each target screen is tracked independently.
/// </summary>
public class CursorMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;

    /// <summary>
    /// Per-screen timestamp of when the cursor was last seen on that screen.
    /// </summary>
    private readonly Dictionary<int, DateTime> _lastSeenOnScreen = new();

    /// <summary>
    /// Screens that currently have an active overlay.
    /// </summary>
    private readonly HashSet<int> _activeOverlayScreens = new();

    /// <summary>
    /// The set of 0-based screen indices being monitored.
    /// </summary>
    public HashSet<int> TargetScreenIndices { get; set; } = new();

    /// <summary>
    /// How many seconds the cursor must be away before the overlay triggers.
    /// </summary>
    public int InactivityTimeoutSeconds { get; set; }

    /// <summary>
    /// Fired when the cursor has been away from a specific target screen
    /// for the configured timeout. The argument is the screen index.
    /// </summary>
    public event Action<int>? ScreenInactivityDetected;

    /// <summary>
    /// Fired when the cursor returns to a specific target screen while
    /// the overlay is active on it. The argument is the screen index.
    /// </summary>
    public event Action<int>? ActivityDetected;

    public CursorMonitor()
    {
        _timer = new System.Windows.Forms.Timer
        {
            Interval = 300 // check every 300ms
        };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>
    /// Starts monitoring cursor position.
    /// </summary>
    public void Start()
    {
        _activeOverlayScreens.Clear();
        _lastSeenOnScreen.Clear();

        // Initialize all target screens with "just seen now"
        DateTime now = DateTime.UtcNow;
        foreach (int idx in TargetScreenIndices)
            _lastSeenOnScreen[idx] = now;

        _timer.Start();
    }

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        int currentScreenIndex = ScreenManager.GetCurrentCursorScreenIndex();
        DateTime now = DateTime.UtcNow;

        // During transient display reconfiguration, cursor position may not map
        // to any screen. Skip this tick so we don't accidentally trigger overlays.
        if (currentScreenIndex < 0)
            return;

        foreach (int idx in TargetScreenIndices)
        {
            bool cursorIsHere = (idx == currentScreenIndex);

            if (cursorIsHere)
            {
                // Cursor is on this target screen — reset its timer
                _lastSeenOnScreen[idx] = now;

                if (_activeOverlayScreens.Contains(idx))
                {
                    // Dismiss overlay for this screen
                    ActivityDetected?.Invoke(idx);
                    _activeOverlayScreens.Remove(idx);
                }
            }
            else
            {
                // Cursor is NOT on this target screen
                if (!_activeOverlayScreens.Contains(idx))
                {
                    if (!_lastSeenOnScreen.ContainsKey(idx))
                        _lastSeenOnScreen[idx] = now;

                    double elapsed = (now - _lastSeenOnScreen[idx]).TotalSeconds;
                    if (elapsed >= InactivityTimeoutSeconds)
                    {
                        _activeOverlayScreens.Add(idx);
                        ScreenInactivityDetected?.Invoke(idx);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Externally marks a screen as having an active overlay so the monitor
    /// will fire <see cref="ActivityDetected"/> when the cursor enters it.
    /// Used by "Black Out Now" which bypasses the inactivity timeout.
    /// </summary>
    public void MarkOverlayActive(int screenIndex)
    {
        _activeOverlayScreens.Add(screenIndex);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
