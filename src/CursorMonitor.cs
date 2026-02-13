namespace BlackScreenSaver;

/// <summary>
/// Monitors the cursor position at regular intervals and raises events
/// when the cursor has been away from the target screen for the configured timeout,
/// or when the cursor returns to the target screen.
/// </summary>
public class CursorMonitor : IDisposable
{
    private readonly System.Windows.Forms.Timer _timer;
    private DateTime _lastSeenOnTargetScreen;
    private bool _overlayActive;

    /// <summary>
    /// The 0-based index of the screen being monitored.
    /// </summary>
    public int TargetScreenIndex { get; set; }

    /// <summary>
    /// How many seconds the cursor must be away before the overlay triggers.
    /// </summary>
    public int InactivityTimeoutSeconds { get; set; }

    /// <summary>
    /// Fired when the cursor has been away from the target screen for the configured timeout.
    /// The argument is the target Screen object.
    /// </summary>
    public event Action<Screen>? InactivityDetected;

    /// <summary>
    /// Fired when the cursor returns to the target screen while the overlay is active.
    /// </summary>
    public event Action? ActivityDetected;

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
        bool cursorOnTarget = currentScreenIndex == TargetScreenIndex;

        if (cursorOnTarget)
        {
            // Cursor is on the target screen — reset timer
            _lastSeenOnTargetScreen = DateTime.UtcNow;

            if (_overlayActive)
            {
                _overlayActive = false;
                ActivityDetected?.Invoke();
            }
        }
        else
        {
            // Cursor is NOT on the target screen
            if (!_overlayActive)
            {
                double elapsed = (DateTime.UtcNow - _lastSeenOnTargetScreen).TotalSeconds;
                if (elapsed >= InactivityTimeoutSeconds)
                {
                    _overlayActive = true;
                    Screen targetScreen = ScreenManager.GetScreen(TargetScreenIndex);
                    InactivityDetected?.Invoke(targetScreen);
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
