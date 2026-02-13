namespace BlackScreenSaver;

/// <summary>
/// A borderless, topmost, full-black window used as the screen overlay.
/// Positioned to exactly cover a single monitor.
/// </summary>
public class OverlayWindow : Form
{
    private Screen? _targetScreen;

    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;

        // Disable auto-scaling so WinForms doesn't resize the form
        // when it enters a monitor with a different DPI (PerMonitorV2).
        AutoScaleMode = AutoScaleMode.None;

        // Ensure the form is created so we can call Show/Hide later
        // without cross-thread issues.
        CreateHandle();
    }

    /// <summary>
    /// Shows the overlay, covering the specified screen entirely.
    /// </summary>
    public void ShowOnScreen(Screen screen)
    {
        if (InvokeRequired)
        {
            Invoke(() => ShowOnScreen(screen));
            return;
        }

        _targetScreen = screen;
        Bounds = screen.Bounds;
        if (!Visible)
        {
            Show();
            // Re-apply bounds after Show() in case the DPI change
            // during window creation altered the size.
            Bounds = screen.Bounds;
        }
    }

    /// <summary>
    /// When the overlay moves to a screen with a different DPI,
    /// force the bounds back to the full screen size.
    /// </summary>
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        if (_targetScreen != null)
        {
            Bounds = _targetScreen.Bounds;
        }
    }

    /// <summary>
    /// Hides the overlay immediately.
    /// </summary>
    public void HideOverlay()
    {
        if (InvokeRequired)
        {
            Invoke(HideOverlay);
            return;
        }

        if (Visible)
        {
            Hide();
        }
    }

    /// <summary>
    /// Prevent the overlay from appearing in Alt+Tab.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    /// <summary>
    /// Prevent closing via Alt+F4 — let the tray icon manage lifetime.
    /// </summary>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideOverlay();
        }
        else
        {
            base.OnFormClosing(e);
        }
    }
}
