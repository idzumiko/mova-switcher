namespace MovaSwitcher;

/// <summary>Registers one system-wide hotkey on a hidden message-only window.</summary>
internal sealed class HotkeyManager : NativeWindow, IDisposable
{
    private const int HotkeyId = 0x4D53;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager()
    {
        CreateHandle(new CreateParams { Parent = Native.HWND_MESSAGE });
    }

    /// <returns>false if another program already owns this combination.</returns>
    public bool Register(Hotkey hotkey)
    {
        Unregister();
        _registered = Native.RegisterHotKey(Handle, HotkeyId, hotkey.Modifiers | Native.MOD_NOREPEAT, (uint)hotkey.Key);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered) return;
        Native.UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY && (int)m.WParam == HotkeyId)
            Pressed?.Invoke();
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        Unregister();
        DestroyHandle();
    }
}
