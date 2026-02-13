namespace BlackScreenSaver;

/// <summary>
/// Settings dialog allowing the user to choose which screen to black out,
/// set the inactivity timeout, and toggle "Start with Windows".
/// </summary>
public class SettingsForm : Form
{
    private readonly ComboBox _screenCombo;
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
        Size = new Size(420, 230);
        ShowInTaskbar = true;
        TopMost = true;

        // --- Screen selection ---
        _screenLabel = new Label
        {
            Text = "Target Screen:",
            Location = new Point(20, 22),
            AutoSize = true
        };

        _screenCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(140, 18),
            Width = 240
        };

        string[] screenNames = ScreenManager.GetAllScreenDisplayNames();
        _screenCombo.Items.AddRange(screenNames);

        int selectedIndex = currentConfig.TargetScreenIndex;
        if (selectedIndex >= 0 && selectedIndex < screenNames.Length)
            _screenCombo.SelectedIndex = selectedIndex;
        else if (screenNames.Length > 0)
            _screenCombo.SelectedIndex = 0;

        // --- Timeout ---
        _timeoutLabel = new Label
        {
            Text = "Timeout:",
            Location = new Point(20, 62),
            AutoSize = true
        };

        _timeoutUpDown = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 300,
            Value = Math.Clamp(currentConfig.InactivityTimeoutSeconds, 1, 300),
            Location = new Point(140, 58),
            Width = 80
        };

        _secondsLabel = new Label
        {
            Text = "seconds",
            Location = new Point(226, 62),
            AutoSize = true
        };

        // --- Start with Windows ---
        _startWithWindowsCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(140, 98),
            AutoSize = true,
            Checked = currentConfig.StartWithWindows
        };

        // --- Buttons ---
        _saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(200, 148),
            Width = 85
        };
        _saveButton.Click += OnSaveClick;

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(295, 148),
            Width = 85
        };

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        Controls.AddRange(new Control[]
        {
            _screenLabel, _screenCombo,
            _timeoutLabel, _timeoutUpDown, _secondsLabel,
            _startWithWindowsCheckBox,
            _saveButton, _cancelButton
        });
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        ResultConfig = new AppConfig
        {
            TargetScreenIndex = _screenCombo.SelectedIndex >= 0 ? _screenCombo.SelectedIndex : 0,
            InactivityTimeoutSeconds = (int)_timeoutUpDown.Value,
            StartWithWindows = _startWithWindowsCheckBox.Checked
        };
    }
}
