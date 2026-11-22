namespace MovaSwitcher;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly Func<Hotkey, bool> _tryRegister;

    private readonly HotkeyBox _hotkeyBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _direction = NewCombo();
    private readonly ComboBox _layoutA = NewCombo();
    private readonly ComboBox _layoutB = NewCombo();
    private readonly CheckBox _switchLayout = NewCheck("Switch keyboard layout after converting");
    private readonly CheckBox _lastWord = NewCheck("If nothing is selected, convert the last word");
    private readonly CheckBox _autostart = NewCheck("Start with Windows");

    /// <param name="tryRegister">Registers a hotkey globally; returns false if it's taken.</param>
    public SettingsForm(AppSettings settings, Func<Hotkey, bool> tryRegister)
    {
        _settings = settings;
        _tryRegister = tryRegister;

        Text = "Mova Switcher — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);
        ShowInTaskbar = true;
        Icon = TrayIcon.Create();

        var grid = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));

        AddRow(grid, "Hotkey:", _hotkeyBox);
        AddRow(grid, "", new Label
        {
            Text = "Click the box and press the combination you want.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 0, 3, 8),
        });
        AddRow(grid, "Convert:", _direction);
        AddRow(grid, "Language 1:", _layoutA);
        AddRow(grid, "Language 2:", _layoutB);
        AddSpanning(grid, _switchLayout);
        AddSpanning(grid, _lastWord);
        AddSpanning(grid, _autostart);

        var save = new Button { Text = "Save", AutoSize = true, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => OnSave();
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(save);
        AddSpanning(grid, buttons);

        Controls.Add(grid);
        AcceptButton = save;
        CancelButton = cancel;

        LoadValues();
    }

    private void LoadValues()
    {
        _hotkeyBox.Hotkey = _settings.Hotkey;

        _direction.Items.AddRange(new object[]
        {
            "From the current layout to the next one",
            "Detect the language from the text",
        });
        _direction.SelectedIndex = (int)_settings.Direction;
        _direction.SelectedIndexChanged += (_, _) => UpdateLanguageRows();

        var installed = KeyboardLayout.GetInstalled();
        _layoutA.Items.AddRange(installed.ToArray<object>());
        _layoutB.Items.AddRange(installed.ToArray<object>());
        if (_settings.ResolveLayouts() is var (a, b))
        {
            _layoutA.SelectedItem = a;
            _layoutB.SelectedItem = b;
        }
        else
        {
            _layoutA.Enabled = _layoutB.Enabled = false;
            if (installed.Count == 1) _layoutA.SelectedIndex = 0;
        }

        _switchLayout.Checked = _settings.SwitchLayoutAfterConvert;
        _lastWord.Checked = _settings.ConvertLastWordIfNoSelection;
        _autostart.Checked = Autostart.IsEnabled;
        UpdateLanguageRows();
    }

    // The language pair only matters when detecting from the text.
    private void UpdateLanguageRows()
    {
        bool pair = _direction.SelectedIndex == (int)ConversionDirection.AutoDetect && KeyboardLayout.GetInstalled().Count >= 2;
        _layoutA.Enabled = _layoutB.Enabled = pair;
    }

    private void OnSave()
    {
        if (_layoutA.Enabled && _layoutA.SelectedItem is KeyboardLayout a && _layoutB.SelectedItem is KeyboardLayout b && a == b)
        {
            MessageBox.Show(this, "Pick two different languages.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var hotkey = _hotkeyBox.Hotkey;
        if (!_tryRegister(hotkey))
        {
            MessageBox.Show(this,
                $"{hotkey} is already used by Windows or another program. Please choose a different combination.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Hotkey = hotkey;
        _settings.Direction = (ConversionDirection)_direction.SelectedIndex;
        _settings.LayoutA = (_layoutA.SelectedItem as KeyboardLayout)?.Id ?? _settings.LayoutA;
        _settings.LayoutB = (_layoutB.SelectedItem as KeyboardLayout)?.Id ?? _settings.LayoutB;
        _settings.SwitchLayoutAfterConvert = _switchLayout.Checked;
        _settings.ConvertLastWordIfNoSelection = _lastWord.Checked;
        _settings.Save();

        try
        {
            Autostart.Set(_autostart.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Couldn't change the startup setting: " + ex.Message, Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 12, 3),
        });
        grid.Controls.Add(control);
    }

    private static void AddSpanning(TableLayoutPanel grid, Control control)
    {
        grid.Controls.Add(control);
        grid.SetColumnSpan(control, 2);
    }

    private static ComboBox NewCombo() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Dock = DockStyle.Fill,
    };

    private static CheckBox NewCheck(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 6, 3, 0),
    };
}
