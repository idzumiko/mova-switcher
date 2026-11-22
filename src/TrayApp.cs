namespace MovaSwitcher;

/// <summary>Lives in the notification area and runs a conversion whenever the hotkey is pressed.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly HotkeyManager _hotkeys = new();
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly Icon _iconOn = TrayIcon.Create(true);
    private readonly Icon _iconOff = TrayIcon.Create(false);

    private SettingsForm? _settingsForm;
    private bool _busy;
    private bool _enabled = true;

    public TrayApp()
    {
        _hotkeys.Pressed += OnHotkeyPressed;

        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => SetEnabled(!_enabled)) { Checked = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => ShowSettings()) { Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(_enabledItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = _iconOn,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowSettings();

        bool registered = _hotkeys.Register(_settings.Hotkey);
        UpdateTooltip();

        if (!registered)
        {
            Notify($"{_settings.Hotkey} is already used by another program. Open Settings to pick a different hotkey.", ToolTipIcon.Warning);
        }
        else if (!_settings.FirstRunShown)
        {
            Notify($"Running in the tray. Select wrongly-typed text and press {_settings.Hotkey}.", ToolTipIcon.Info);
            _settings.FirstRunShown = true;
            _settings.Save();
        }

        if (_settings.ResolveLayouts() == null)
            Notify("Only one keyboard layout is installed. Add a second language in Windows Settings → Time & Language.", ToolTipIcon.Warning);
    }

    private async void OnHotkeyPressed()
    {
        if (!_enabled || _busy) return;

        // Stop the Alt/Win release from opening a menu bar or the Start menu.
        if (InputSender.AltOrWinDown()) InputSender.SuppressMenuActivation();

        var layouts = _settings.ResolveLayouts();
        if (layouts is not var (a, b))
        {
            Notify("Add a second keyboard layout in Windows settings first.", ToolTipIcon.Warning);
            return;
        }

        _busy = true;
        try
        {
            await TextSwapper.ConvertSelectionAsync(_settings, a, b);
        }
        catch (Exception ex)
        {
            Notify("Conversion failed: " + ex.Message, ToolTipIcon.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm != null)
        {
            _settingsForm.Activate();
            return;
        }

        // Release the hotkey while the dialog is open so the user can record it (or a new one).
        _hotkeys.Unregister();
        _settingsForm = new SettingsForm(_settings, hk => _hotkeys.Register(hk));
        _settingsForm.FormClosed += (_, _) =>
        {
            _settingsForm = null;
            _hotkeys.Register(_settings.Hotkey);
            UpdateTooltip();
        };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _enabledItem.Checked = enabled;
        _tray.Icon = enabled ? _iconOn : _iconOff;
        UpdateTooltip();
    }

    private void UpdateTooltip()
    {
        string text = _enabled ? $"Mova Switcher — {_settings.Hotkey}" : "Mova Switcher — paused";
        _tray.Text = text.Length > 63 ? text[..63] : text; // NotifyIcon tooltip limit
    }

    private void Notify(string message, ToolTipIcon icon) =>
        _tray.ShowBalloonTip(5000, "Mova Switcher", message, icon);

    protected override void ExitThreadCore()
    {
        _settingsForm?.Close();
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        base.ExitThreadCore();
    }
}
