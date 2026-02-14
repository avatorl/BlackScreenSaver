using System.Runtime.InteropServices;

namespace BlackScreenSaver;

/// <summary>
/// A borderless, topmost, full-black window used as the screen overlay.
/// Positioned to exactly cover a single monitor.
/// </summary>
public class OverlayWindow : Form
{
    private Screen? _targetScreen;

    private const int SW_SHOWNOACTIVATE = 4;
    private const int HWND_TOPMOST = -1;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

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
            // Show without stealing focus from the user's active window.
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
            Visible = true;

            // Ensure topmost positioning without activation.
            SetWindowPos(Handle, (IntPtr)HWND_TOPMOST,
                screen.Bounds.X, screen.Bounds.Y,
                screen.Bounds.Width, screen.Bounds.Height,
                SWP_NOACTIVATE);
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
            const int WS_EX_NOACTIVATE = 0x08000000;
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
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
