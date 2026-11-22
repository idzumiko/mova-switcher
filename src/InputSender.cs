using System.Runtime.InteropServices;

namespace MovaSwitcher;

/// <summary>Synthesizes keystrokes into whatever window has focus.</summary>
internal static class InputSender
{
    private const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private static readonly int[] ModifierKeys = { VK_SHIFT, VK_CONTROL, VK_MENU, VK_LWIN, VK_RWIN };

    // An unassigned virtual-key code. Tapping it while Alt/Win is held stops Windows from
    // treating the later Alt/Win release as "open the menu bar / Start menu".
    private const ushort VK_UNASSIGNED = 0xE8;

    public static void Ctrl(Keys key) => Combo(key, ctrl: true, shift: false);

    public static void CtrlShift(Keys key) => Combo(key, ctrl: true, shift: true);

    public static void Combo(Keys key, bool ctrl, bool shift)
    {
        var inputs = new List<Native.INPUT>();
        if (ctrl) inputs.Add(KeyInput(VK_CONTROL, up: false));
        if (shift) inputs.Add(KeyInput(VK_SHIFT, up: false));
        inputs.Add(KeyInput((ushort)key, up: false));
        inputs.Add(KeyInput((ushort)key, up: true));
        if (shift) inputs.Add(KeyInput(VK_SHIFT, up: true));
        if (ctrl) inputs.Add(KeyInput(VK_CONTROL, up: true));
        Send(inputs);
    }

    public static void Tap(Keys key) =>
        Send(new List<Native.INPUT> { KeyInput((ushort)key, false), KeyInput((ushort)key, true) });

    public static bool IsDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;

    public static bool AltOrWinDown() => IsDown(VK_MENU) || IsDown(VK_LWIN) || IsDown(VK_RWIN);

    public static void SuppressMenuActivation() =>
        Send(new List<Native.INPUT> { KeyInput(VK_UNASSIGNED, false), KeyInput(VK_UNASSIGNED, true) });

    /// <summary>
    /// Wait until the user lets go of the hotkey, so our synthetic Ctrl+C isn't seen as
    /// e.g. Ctrl+Shift+C. If keys are still held after the timeout, release them ourselves.
    /// </summary>
    public static async Task WaitForKeysReleasedAsync(Keys hotkeyKey, int timeoutMs = 1500)
    {
        var watched = ModifierKeys.Append((int)hotkeyKey).ToArray();
        var deadline = Environment.TickCount64 + timeoutMs;
        while (watched.Any(IsDown) && Environment.TickCount64 < deadline)
            await Task.Delay(15);

        var stuck = ModifierKeys.Where(IsDown).Select(vk => KeyInput((ushort)vk, up: true)).ToList();
        if (stuck.Count > 0) Send(stuck);
    }

    private static Native.INPUT KeyInput(ushort vk, bool up)
    {
        uint flags = up ? Native.KEYEVENTF_KEYUP : 0;
        if (IsExtended(vk)) flags |= Native.KEYEVENTF_EXTENDEDKEY;
        return new Native.INPUT
        {
            type = Native.INPUT_KEYBOARD,
            U = new Native.InputUnion
            {
                ki = new Native.KEYBDINPUT
                {
                    wVk = vk,
                    wScan = (ushort)Native.MapVirtualKey(vk, Native.MAPVK_VK_TO_VSC),
                    dwFlags = flags,
                },
            },
        };
    }

    private static bool IsExtended(ushort vk) => (Keys)vk is
        Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or
        Keys.PageUp or Keys.PageDown or Keys.Insert or Keys.Delete or Keys.RWin or Keys.LWin;

    private static void Send(List<Native.INPUT> inputs) =>
        Native.SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Native.INPUT>());
}
