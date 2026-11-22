namespace MovaSwitcher;

/// <summary>A text box that records the key combination the user presses.</summary>
internal sealed class HotkeyBox : TextBox
{
    private Hotkey _hotkey = new();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Hotkey Hotkey
    {
        get => _hotkey.Clone();
        set { _hotkey = value.Clone(); Text = _hotkey.ToString(); }
    }

    public HotkeyBox()
    {
        ReadOnly = true;
        BackColor = SystemColors.Window;
        Cursor = Cursors.Hand;
        ShortcutsEnabled = false;
    }

    // Let Tab, arrows, etc. reach OnKeyDown instead of moving focus.
    protected override bool IsInputKey(Keys keyData) => true;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Alt combinations arrive as system keys; route them through OnKeyDown too.
        OnKeyDown(new KeyEventArgs(keyData));
        return true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        Keys key = e.KeyCode;
        bool win = InputSender.IsDown(0x5B) || InputSender.IsDown(0x5C);

        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            // Only modifiers so far: show a live preview.
            var preview = new List<string>();
            if (e.Control) preview.Add("Ctrl");
            if (e.Shift) preview.Add("Shift");
            if (e.Alt) preview.Add("Alt");
            if (win) preview.Add("Win");
            Text = string.Join(" + ", preview) + " + …";
            return;
        }

        bool noModifiers = !e.Control && !e.Shift && !e.Alt && !win;
        if (noModifiers && key is Keys.Back or Keys.Escape)
        {
            Text = _hotkey.ToString(); // cancel the edit
            return;
        }

        // A plain letter/digit with no modifier would hijack normal typing; require a modifier
        // unless it's a key you don't type with (Pause, F-keys, ScrollLock...).
        bool typingKey = key is (>= Keys.A and <= Keys.Z) or (>= Keys.D0 and <= Keys.D9) or Keys.Space or Keys.Enter or Keys.Tab
            || (key >= Keys.OemSemicolon && key <= Keys.OemBackslash);
        if (noModifiers && typingKey)
        {
            Text = "Add Ctrl, Alt, Shift or Win to that key";
            return;
        }

        _hotkey = new Hotkey { Key = key, Ctrl = e.Control, Shift = e.Shift, Alt = e.Alt, Win = win };
        Text = _hotkey.ToString();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        e.Handled = true;
        if (Text.EndsWith("…")) Text = _hotkey.ToString();
    }

    protected override void OnLeave(EventArgs e)
    {
        Text = _hotkey.ToString();
        base.OnLeave(e);
    }
}
