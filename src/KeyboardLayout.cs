using System.Globalization;

namespace MovaSwitcher;

/// <summary>A physical key plus Shift state.</summary>
public readonly record struct KeyStroke(uint Vk, bool Shift);

/// <summary>
/// One installed Windows keyboard layout, with a two-way map between the characters
/// it produces and the physical keys that produce them. Built by asking Windows
/// (ToUnicodeEx) what each key types, so it works for any layout pair, not only UA/EN.
/// </summary>
public sealed class KeyboardLayout
{
    // Keys that type text: space, digits, letters and the OEM punctuation keys.
    // Numpad keys are skipped so that "." maps to the main-keyboard key.
    private static readonly uint[] TextKeys = BuildTextKeys();

    private readonly Dictionary<char, KeyStroke> _charToKey = new();
    private readonly Dictionary<KeyStroke, char> _keyToChar = new();

    public IntPtr Handle { get; }
    public string Id => ((long)Handle).ToString("X8");
    public string LanguageName { get; }
    public string DisplayName { get; internal set; }

    private KeyboardLayout(IntPtr hkl)
    {
        Handle = hkl;
        int langId = (int)((long)hkl & 0xFFFF);
        try
        {
            LanguageName = CultureInfo.GetCultureInfo(langId).DisplayName;
        }
        catch (CultureNotFoundException)
        {
            LanguageName = $"Layout {Id}";
        }
        DisplayName = LanguageName;
        BuildMaps();
    }

    public bool Contains(char c) => _charToKey.ContainsKey(c);
    public bool TryGetKey(char c, out KeyStroke key) => _charToKey.TryGetValue(c, out key);
    public bool TryGetChar(KeyStroke key, out char c) => _keyToChar.TryGetValue(key, out c);

    public override string ToString() => DisplayName;

    private static readonly Dictionary<IntPtr, KeyboardLayout> Cache = new();

    /// <summary>Layouts currently installed for the user, in the order Windows lists them.</summary>
    public static IReadOnlyList<KeyboardLayout> GetInstalled()
    {
        int count = Native.GetKeyboardLayoutList(0, null);
        var handles = new IntPtr[count];
        count = Native.GetKeyboardLayoutList(count, handles);

        var result = new List<KeyboardLayout>(count);
        for (int i = 0; i < count; i++)
        {
            if (!Cache.TryGetValue(handles[i], out var layout))
            {
                layout = new KeyboardLayout(handles[i]);
                Cache[handles[i]] = layout;
            }
            result.Add(layout);
        }

        // Disambiguate two layouts for the same language (e.g. "Ukrainian" and "Ukrainian (Enhanced)").
        foreach (var group in result.GroupBy(l => l.LanguageName))
        {
            bool dup = group.Count() > 1;
            foreach (var l in group)
                l.DisplayName = dup ? $"{l.LanguageName} [{l.Id}]" : l.LanguageName;
        }
        return result;
    }

    private void BuildMaps()
    {
        var state = new byte[256];
        var buffer = new char[8];
        const byte down = 0x80;
        const int VK_SHIFT = 0x10;
        // Flag 0x4: don't change the keyboard state (so dead keys don't leak into the next call).
        const uint flagsNoStateChange = 0x4;

        foreach (bool shift in new[] { false, true })
        {
            Array.Clear(state);
            if (shift) state[VK_SHIFT] = down;

            foreach (uint vk in TextKeys)
            {
                uint scan = Native.MapVirtualKeyEx(vk, Native.MAPVK_VK_TO_VSC, Handle);
                int n = Native.ToUnicodeEx(vk, scan, state, buffer, buffer.Length, flagsNoStateChange, Handle);
                // n == 1: ordinary character; n == -1: dead key (buffer holds its spacing form).
                if (n != 1 && n != -1) continue;

                char c = buffer[0];
                if (char.IsControl(c)) continue;

                var key = new KeyStroke(vk, shift);
                _keyToChar[key] = c;
                _charToKey.TryAdd(c, key); // unshifted wins if a char appears twice
            }
        }
    }

    private static uint[] BuildTextKeys()
    {
        var keys = new List<uint> { 0x20 };                // space
        for (uint vk = 0x30; vk <= 0x39; vk++) keys.Add(vk); // 0-9
        for (uint vk = 0x41; vk <= 0x5A; vk++) keys.Add(vk); // A-Z
        for (uint vk = 0xBA; vk <= 0xC0; vk++) keys.Add(vk); // ;=,-./`
        for (uint vk = 0xDB; vk <= 0xDF; vk++) keys.Add(vk); // [\]' and OEM_8
        keys.Add(0xE2);                                      // OEM_102 (<> on ISO keyboards)
        return keys.ToArray();
    }
}
