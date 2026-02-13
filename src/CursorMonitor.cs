namespace BlackScreenSaver;

/// <summary>
/// Monitors the cursor position at regular intervals and raises events
/// when the cursor has been away from all target screens for the configured timeout,
/// or when the cursor returns to any target screen.
/// </summary>
public class CursorMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _lastSeenOnTargetScreen;
    private bool _overlayActive;
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
    /// Fired when the cursor has been away from all target screens for the configured timeout.
    /// The argument is the list of target Screen objects.
    /// </summary>
    public event Action<IReadOnlyList<Screen>>? InactivityDetected;

    /// <summary>
    /// Fired when the cursor returns to a specific target screen while the overlay is active.
    /// The argument is the screen index where the cursor entered.
    /// </summary>
    public event Action<int>? ActivityDetected;

    public CursorMonitor()
    {
        _lastSeenOnTargetScreen = DateTime.UtcNow;
        _overlayActive = false;

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
        _lastSeenOnTargetScreen = DateTime.UtcNow;
        _overlayActive = false;
        _activeOverlayScreens.Clear();
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
        bool cursorOnTarget = TargetScreenIndices.Contains(currentScreenIndex);

        if (cursorOnTarget)
        {
            // Cursor is on one of the target screens — reset timer
            _lastSeenOnTargetScreen = DateTime.UtcNow;

            if (_overlayActive)
            {
                // Remove overlay only from the screen the cursor entered
                ActivityDetected?.Invoke(currentScreenIndex);
                _activeOverlayScreens.Remove(currentScreenIndex);

                // Reset overlay state when all screen overlays have been dismissed
                if (_activeOverlayScreens.Count == 0)
                    _overlayActive = false;
            }
        }
        else
        {
            // Cursor is NOT on any target screen
            if (!_overlayActive)
            {
                double elapsed = (DateTime.UtcNow - _lastSeenOnTargetScreen).TotalSeconds;
                if (elapsed >= InactivityTimeoutSeconds)
                {
                    _overlayActive = true;
                    _activeOverlayScreens.Clear();
                    foreach (int idx in TargetScreenIndices)
                        _activeOverlayScreens.Add(idx);
                    List<Screen> targetScreens = ScreenManager.GetScreens(TargetScreenIndices);
                    InactivityDetected?.Invoke(targetScreens);
                }
            }
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
