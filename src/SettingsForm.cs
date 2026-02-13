namespace BlackScreenSaver;

/// <summary>
/// Settings dialog allowing the user to choose which screens to black out,
/// set the inactivity timeout, and toggle "Start with Windows".
/// </summary>
public class SettingsForm : Form
{
    private readonly ScreenLayoutPanel _screenPanel;
    private readonly NumericUpDown _timeoutUpDown;
    private readonly CheckBox _startWithWindowsCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;
    private readonly Label _screenLabel;
    private readonly Label _timeoutLabel;
    private readonly Label _secondsLabel;

    public AppConfig ResultConfig { get; private set; }

    public SettingsForm(AppConfig currentConfig)
    {
        ResultConfig = currentConfig;

        Text = "Black Screen Saver – Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        TopMost = true;

        // --- Compute panel height based on screen layout aspect ratio ---
        int panelWidth = 390;
        int panelHeight = ComputePanelHeight(panelWidth);
        int panelTop = 36;
        int panelBottom = panelTop + panelHeight;

        // --- Screen layout panel ---
        _screenLabel = new Label
        {
            Text = "Click screens to select for blackout:",
            Location = new Point(20, 14),
            AutoSize = true,
            ForeColor = Color.FromArgb(80, 80, 80)
        };

        int primaryIdx = ScreenManager.GetPrimaryScreenIndex();
        var selectedIndices = new HashSet<int>(currentConfig.TargetScreenIndices ?? new List<int> { 1 });
        selectedIndices.Remove(primaryIdx); // primary screen can never be selected

        _screenPanel = new ScreenLayoutPanel
        {
            Location = new Point(20, panelTop),
            Size = new Size(panelWidth, panelHeight),
            BorderStyle = BorderStyle.FixedSingle,
            SelectedIndices = selectedIndices,
            LockedIndices = new HashSet<int> { primaryIdx }
        };

        // --- Timeout ---
        int row1 = panelBottom + 18;
        _timeoutLabel = new Label
        {
            Text = "Timeout:",
            Location = new Point(20, row1 + 4),
            AutoSize = true
        };

        _timeoutUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 9999,
            Value = Math.Max(currentConfig.InactivityTimeoutSeconds, 1),
            Location = new Point(140, row1),
            Width = 80
        };
        _timeoutUpDown.ValueChanged += OnTimeoutValueChanged;
        _timeoutUpDown.Leave += OnTimeoutLeave;
        UpdateTimeoutColor();

        _secondsLabel = new Label
        {
            Text = "seconds",
            Location = new Point(226, row1 + 4),
            AutoSize = true
        };

        // --- Start with Windows ---
        int row2 = row1 + 38;
        _startWithWindowsCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(140, row2),
            AutoSize = true,
            Checked = currentConfig.StartWithWindows
        };

        // --- Buttons ---
        int buttonRow = row2 + 40;
        _saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(220, buttonRow),
            Width = 85
        };
        _saveButton.Click += OnSaveClick;

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(315, buttonRow),
            Width = 85
        };

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        // Set form size to fit all controls
        Size = new Size(440, buttonRow + 75);

        Controls.AddRange(new Control[]
        {
            _screenLabel, _screenPanel,
            _timeoutLabel, _timeoutUpDown, _secondsLabel,
            _startWithWindowsCheckBox,
            _saveButton, _cancelButton
        });
    }

    /// <summary>
    /// Computes the ideal panel height based on the aspect ratio of
    /// the combined screen bounding box. Clamped to [100, 300].
    /// </summary>
    private static int ComputePanelHeight(int panelWidth)
    {
        Screen[] screens = Screen.AllScreens;
        if (screens.Length == 0)
            return 160;

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
        if (totalW <= 0) return 160;

        float aspect = totalH / totalW;
        int height = (int)(panelWidth * aspect);

        // Clamp to reasonable range
        return Math.Clamp(height, 100, 300);
    }

    private void OnTimeoutValueChanged(object? sender, EventArgs e)
    {
        UpdateTimeoutColor();
    }

    private void OnTimeoutLeave(object? sender, EventArgs e)
    {
        if (_timeoutUpDown.Value > 300)
            _timeoutUpDown.Value = 300;
        UpdateTimeoutColor();
    }

    private void UpdateTimeoutColor()
    {
        _timeoutUpDown.ForeColor = _timeoutUpDown.Value > 300 ? Color.Red : SystemColors.WindowText;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        var selectedIndices = _screenPanel.SelectedIndices.ToList();
        if (selectedIndices.Count == 0)
        {
            MessageBox.Show("Please select at least one screen.", "Settings",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        // Clamp timeout to 300 on save as well
        if (_timeoutUpDown.Value > 300)
            _timeoutUpDown.Value = 300;

        selectedIndices.Sort();
        ResultConfig = new AppConfig
        {
            TargetScreenIndices = selectedIndices,
            InactivityTimeoutSeconds = (int)_timeoutUpDown.Value,
            StartWithWindows = _startWithWindowsCheckBox.Checked
        };
    }
}
