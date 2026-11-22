using System.Runtime.InteropServices;

namespace MovaSwitcher;

/// <summary>
/// The actual "fix my text" action: copy the selection, convert it, paste it back,
/// restore the user's clipboard and (optionally) switch the active layout.
/// </summary>
internal static class TextSwapper
{
    public enum Outcome { Converted, NothingSelected, NothingToChange }

    public static async Task<Outcome> ConvertSelectionAsync(AppSettings settings, KeyboardLayout a, KeyboardLayout b)
    {
        // Read the active layout of the window the user is typing in before we touch anything.
        IntPtr currentLayout = GetForegroundLayout();

        await InputSender.WaitForKeysReleasedAsync(settings.Hotkey.Key);

        var backup = ClipboardHelper.Save();
        try
        {
            string? text = await CopySelectionAsync();

            if (string.IsNullOrEmpty(text) && settings.ConvertLastWordIfNoSelection)
            {
                InputSender.CtrlShift(Keys.Left); // select the word left of the cursor
                await Task.Delay(40);
                text = await CopySelectionAsync();
            }

            if (string.IsNullOrEmpty(text))
                return Outcome.NothingSelected;

            var result = settings.Direction == ConversionDirection.CurrentToNext
                ? LayoutConverter.ConvertFromCurrent(text, KeyboardLayout.GetInstalled(), currentLayout)
                : LayoutConverter.Convert(text, a, b);
            if (result.Text == text)
                return Outcome.NothingToChange;

            ClipboardHelper.SetText(result.Text);
            InputSender.Ctrl(Keys.V);
            // The target app reads the clipboard asynchronously; don't restore too early.
            await Task.Delay(300);

            if (settings.SwitchLayoutAfterConvert)
                SwitchForegroundLayout(result.Target.Handle);

            return Outcome.Converted;
        }
        finally
        {
            ClipboardHelper.Restore(backup);
        }
    }

    private static async Task<string?> CopySelectionAsync()
    {
        uint seq = Native.GetClipboardSequenceNumber();
        InputSender.Ctrl(Keys.C);
        return await ClipboardHelper.WaitForNewTextAsync(seq, timeoutMs: 400);
    }

    private static IntPtr GetForegroundLayout()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        uint thread = Native.GetWindowThreadProcessId(foreground, IntPtr.Zero);
        return Native.GetKeyboardLayout(thread);
    }

    /// <summary>Ask the focused window to switch its input language.</summary>
    public static void SwitchForegroundLayout(IntPtr hkl)
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;

        uint thread = Native.GetWindowThreadProcessId(foreground, IntPtr.Zero);
        if (Native.GetKeyboardLayout(thread) == hkl) return;

        var info = new Native.GUITHREADINFO { cbSize = Marshal.SizeOf<Native.GUITHREADINFO>() };
        IntPtr target = Native.GetGUIThreadInfo(thread, ref info) && info.hwndFocus != IntPtr.Zero
            ? info.hwndFocus
            : foreground;

        Native.PostMessage(target, Native.WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hkl);
    }
}
