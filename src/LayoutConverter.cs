using System.Text;

namespace MovaSwitcher;

public sealed record ConversionResult(string Text, KeyboardLayout Source, KeyboardLayout Target);

/// <summary>
/// Re-types text as if the same physical keys had been pressed in the other layout:
/// "ghbdsn" (typed with EN active) becomes "привіт", and "руддщ" becomes "hello".
/// </summary>
public static class LayoutConverter
{
    /// <summary>Auto-detect mode: work out from the text which of the two layouts it was typed in.</summary>
    public static ConversionResult Convert(string text, KeyboardLayout a, KeyboardLayout b)
    {
        var (source, target) = DetectSource(text, a, b);
        return Retype(text, source, target);
    }

    /// <summary>
    /// Current-layout mode: the text was typed in the layout that's active now, so convert it
    /// to the next installed layout (pressing again keeps cycling when you have 3+ layouts).
    /// If the active layout can't type any of the text's letters (you already switched by hand),
    /// fall back to the layout that types the most of them.
    /// </summary>
    public static ConversionResult ConvertFromCurrent(string text, IReadOnlyList<KeyboardLayout> installed, IntPtr currentLayout)
    {
        int index = Math.Max(0, installed.ToList().FindIndex(l => l.Handle == currentLayout));

        if (text.Any(char.IsLetter) && LettersTypeable(text, installed[index]) == 0)
        {
            var best = installed.MaxBy(l => LettersTypeable(text, l))!;
            if (LettersTypeable(text, best) > 0)
                index = installed.ToList().IndexOf(best);
        }

        var source = installed[index];
        var target = installed[(index + 1) % installed.Count];
        return Retype(text, source, target);
    }

    private static int LettersTypeable(string text, KeyboardLayout layout) =>
        text.Count(c => char.IsLetter(c) && layout.Contains(c));

    public static ConversionResult Retype(string text, KeyboardLayout source, KeyboardLayout target)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (source.TryGetKey(c, out var key) && target.TryGetChar(key, out char converted))
                sb.Append(converted);
            else
                sb.Append(c); // emoji, line breaks, characters neither layout can type, etc.
        }
        return new ConversionResult(sb.ToString(), source, target);
    }

    /// <summary>
    /// Decide which layout the text was typed in by counting characters that only one
    /// of the two layouts can produce (Cyrillic letters vs Latin letters, for example).
    /// Characters both layouts share (digits, space) don't vote. A tie converts a → b.
    /// </summary>
    public static (KeyboardLayout source, KeyboardLayout target) DetectSource(string text, KeyboardLayout a, KeyboardLayout b)
    {
        int onlyA = 0, onlyB = 0;
        foreach (char c in text)
        {
            bool inA = a.Contains(c), inB = b.Contains(c);
            if (inA && !inB) onlyA++;
            else if (inB && !inA) onlyB++;
        }
        return onlyB > onlyA ? (b, a) : (a, b);
    }
}
