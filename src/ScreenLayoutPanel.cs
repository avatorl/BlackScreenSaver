namespace BlackScreenSaver;

/// <summary>
/// A custom panel that draws a miniature layout of all connected monitors
/// in their real relative positions (like Windows Display Settings).
/// Clicking a screen rectangle toggles its selection.
/// </summary>
public class ScreenLayoutPanel : Panel
{
    private readonly List<RectangleF> _scaledRects = new();
    private readonly List<int> _screenIndices = new();

    /// <summary>
    /// The set of currently selected screen indices.
    /// </summary>
    public HashSet<int> SelectedIndices { get; set; } = new();

    // Colors
    private static readonly Color SelectedFill = Color.FromArgb(40, 100, 210);
    private static readonly Color SelectedBorder = Color.FromArgb(60, 130, 255);
    private static readonly Color UnselectedFill = Color.FromArgb(60, 60, 60);
    private static readonly Color UnselectedBorder = Color.FromArgb(100, 100, 100);
    private static readonly Color HoverFill = Color.FromArgb(80, 80, 80);
    private static readonly Color PanelBackground = Color.FromArgb(30, 30, 30);

    private static readonly Color LockedFill = Color.FromArgb(45, 45, 45);
    private static readonly Color LockedBorder = Color.FromArgb(80, 80, 80);

    private int _hoverIndex = -1;

    /// <summary>
    /// Screen indices that cannot be selected (e.g. the primary screen).
    /// </summary>
    public HashSet<int> LockedIndices { get; set; } = new();

    public ScreenLayoutPanel()
    {
        DoubleBuffered = true;
        BackColor = PanelBackground;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    /// <summary>
    /// Recalculates the scaled rectangles for all screens to fit within the panel.
    /// </summary>
    private void RecalculateLayout()
    {
        _scaledRects.Clear();
        _screenIndices.Clear();

        Screen[] screens = Screen.AllScreens;
        if (screens.Length == 0) return;

        // Find bounding box of all screens in virtual desktop coords
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (Screen s in screens)
        {
            if (s.Bounds.Left < minX) minX = s.Bounds.Left;
            if (s.Bounds.Top < minY) minY = s.Bounds.Top;
            if (s.Bounds.Right > maxX) maxX = s.Bounds.Right;
            if (s.Bounds.Bottom > maxY) maxY = s.Bounds.Bottom;
        }

        float totalW = maxX - minX;
        float totalH = maxY - minY;
        if (totalW <= 0 || totalH <= 0) return;

        // Scale to fit the panel with padding
        float padding = 16;
        float availW = Width - padding * 2;
        float availH = Height - padding * 2;

        float scale = Math.Min(availW / totalW, availH / totalH);

        // Center the drawing
        float scaledTotalW = totalW * scale;
        float scaledTotalH = totalH * scale;
        float offsetX = (Width - scaledTotalW) / 2f;
        float offsetY = (Height - scaledTotalH) / 2f;

        for (int i = 0; i < screens.Length; i++)
        {
            Rectangle b = screens[i].Bounds;
            float x = (b.X - minX) * scale + offsetX;
            float y = (b.Y - minY) * scale + offsetY;
            float w = b.Width * scale;
            float h = b.Height * scale;

            // Slight inset so adjacent screens have a visible gap
            _scaledRects.Add(new RectangleF(x + 2, y + 2, w - 4, h - 4));
            _screenIndices.Add(i);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        RecalculateLayout();

        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        Screen[] screens = Screen.AllScreens;

        for (int i = 0; i < _scaledRects.Count; i++)
        {
            RectangleF rect = _scaledRects[i];
            int screenIdx = _screenIndices[i];
            bool locked = LockedIndices.Contains(screenIdx);
            bool selected = !locked && SelectedIndices.Contains(screenIdx);
            bool hovered = !locked && _hoverIndex == screenIdx;

            // Fill
            Color fill = locked ? LockedFill : selected ? SelectedFill : (hovered ? HoverFill : UnselectedFill);
            using var brush = new SolidBrush(fill);
            g.FillRectangle(brush, rect);

            // Border
            Color border = locked ? LockedBorder : selected ? SelectedBorder : UnselectedBorder;
            using var pen = new Pen(border, selected ? 2f : 1f);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            // Labels
            string label = (screenIdx + 1).ToString();
            string sub = screens[screenIdx].Primary ? "Primary" : "";
            string res = $"{screens[screenIdx].Bounds.Width}×{screens[screenIdx].Bounds.Height}";

            using var labelFont = new Font("Segoe UI", Math.Max(rect.Height * 0.22f, 10f), FontStyle.Bold);
            using var subFont = new Font("Segoe UI", Math.Max(rect.Height * 0.11f, 7f), FontStyle.Regular);
            using var labelBrush = new SolidBrush(selected ? Color.White : Color.FromArgb(180, 180, 180));
            using var subBrush = new SolidBrush(selected ? Color.FromArgb(200, 220, 255) : Color.FromArgb(130, 130, 130));

            var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            // Screen number — centered, slightly above middle
            var labelRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height * 0.65f);
            g.DrawString(label, labelFont, labelBrush, labelRect, sf);

            // Sub-label (Primary / resolution) — below middle
            if (locked) sub = "Primary (locked)";
            string info = string.IsNullOrEmpty(sub) ? res : $"{sub}\n{res}";
            var subRect = new RectangleF(rect.X, rect.Y + rect.Height * 0.50f, rect.Width, rect.Height * 0.45f);
            g.DrawString(info, subFont, subBrush, subRect, sf);
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);

        for (int i = 0; i < _scaledRects.Count; i++)
        {
            if (_scaledRects[i].Contains(e.Location))
            {
                int idx = _screenIndices[i];

                // Primary screen cannot be selected
                if (LockedIndices.Contains(idx))
                    return;

                if (SelectedIndices.Contains(idx))
                    SelectedIndices.Remove(idx);
                else
                    SelectedIndices.Add(idx);

                Invalidate();
                return;
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        int newHover = -1;
        for (int i = 0; i < _scaledRects.Count; i++)
        {
            if (_scaledRects[i].Contains(e.Location))
            {
                newHover = _screenIndices[i];
                break;
            }
        }

        if (newHover != _hoverIndex)
        {
            _hoverIndex = newHover;
            bool isLocked = _hoverIndex >= 0 && LockedIndices.Contains(_hoverIndex);
            Cursor = _hoverIndex >= 0 && !isLocked ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex >= 0)
        {
            _hoverIndex = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }
    }
}
