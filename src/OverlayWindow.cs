namespace BlackScreenSaver;

/// <summary>
/// A borderless, topmost, full-black window used as the screen overlay.
/// Positioned to exactly cover a single monitor.
/// </summary>
public class OverlayWindow : Form
{
    public OverlayWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;

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

        Bounds = screen.Bounds;
        if (!Visible)
        {
            Show();
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
