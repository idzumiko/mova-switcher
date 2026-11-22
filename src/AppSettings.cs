using System.Text.Json;
using System.Text.Json.Serialization;

namespace MovaSwitcher;

/// <summary>A global key combination, e.g. Ctrl + Shift + Space.</summary>
public sealed class Hotkey
{
    public Keys Key { get; set; } = Keys.Space;
    public bool Ctrl { get; set; } = true;
    public bool Shift { get; set; } = true;
    public bool Alt { get; set; }
    public bool Win { get; set; }

    [JsonIgnore]
    public uint Modifiers =>
        (Ctrl ? Native.MOD_CONTROL : 0) | (Shift ? Native.MOD_SHIFT : 0) |
        (Alt ? Native.MOD_ALT : 0) | (Win ? Native.MOD_WIN : 0);

    public Hotkey Clone() => (Hotkey)MemberwiseClone();

    public override string ToString()
    {
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        if (Win) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join(" + ", parts);
    }

    public static string KeyName(Keys key) => key switch
    {
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        Keys.Oemtilde => "`",
        Keys.OemMinus => "-",
        Keys.Oemplus => "=",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe => "\\",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.Oemcomma => ",",
        Keys.OemPeriod => ".",
        Keys.OemQuestion => "/",
        Keys.OemBackslash => "\\ (102)",
        Keys.Next => "PageDown",
        Keys.Prior => "PageUp",
        Keys.Capital => "CapsLock",
        Keys.Scroll => "ScrollLock",
        _ => key.ToString(),
    };
}

public enum ConversionDirection
{
    /// <summary>Text was typed in the active layout; convert it to the next installed one.</summary>
    CurrentToNext,
    /// <summary>Guess from the characters which of the two chosen languages the text is in.</summary>
    AutoDetect,
}

public sealed class AppSettings
{
    public Hotkey Hotkey { get; set; } = new();

    public ConversionDirection Direction { get; set; } = ConversionDirection.CurrentToNext;

    /// <summary>Auto-detect mode only. Layout ids (HKL as hex). Null = the first two installed layouts.</summary>
    public string? LayoutA { get; set; }
    public string? LayoutB { get; set; }

    /// <summary>After converting, switch the active keyboard layout to the target language.</summary>
    public bool SwitchLayoutAfterConvert { get; set; } = true;

    /// <summary>If nothing is selected, select and convert the word left of the cursor.</summary>
    public bool ConvertLastWordIfNoSelection { get; set; } = true;

    public bool FirstRunShown { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MovaSwitcher", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch
        {
            // Corrupt file: fall back to defaults rather than refusing to start.
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Resolve the configured pair against what's installed now.</summary>
    public (KeyboardLayout a, KeyboardLayout b)? ResolveLayouts()
    {
        var installed = KeyboardLayout.GetInstalled();
        if (installed.Count < 2) return null;

        var a = installed.FirstOrDefault(l => l.Id == LayoutA) ?? installed[0];
        var b = installed.FirstOrDefault(l => l.Id == LayoutB && l != a) ?? installed.First(l => l != a);
        return (a, b);
    }
}
