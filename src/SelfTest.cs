namespace MovaSwitcher;

/// <summary>
/// `MovaSwitcher.exe --selftest [text...]` prints the installed layouts and converts
/// each argument with the configured pair. Handy for checking a new layout pair.
/// </summary>
internal static class SelfTest
{
    public static int Run(string[] samples)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var installed = KeyboardLayout.GetInstalled();
        Console.WriteLine("Installed layouts:");
        foreach (var l in installed) Console.WriteLine($"  {l.Id}  {l.DisplayName}");

        if (AppSettings.Load().ResolveLayouts() is not var (a, b))
        {
            Console.WriteLine("Need at least two layouts to convert.");
            return 1;
        }

        Console.WriteLine($"Auto-detect, pair {a.DisplayName} <-> {b.DisplayName}:");
        foreach (var s in samples)
            Print(s, LayoutConverter.Convert(s, a, b));

        foreach (var current in installed)
        {
            Console.WriteLine($"Current layout = {current.DisplayName}:");
            foreach (var s in samples)
                Print(s, LayoutConverter.ConvertFromCurrent(s, installed, current.Handle));
        }
        return 0;

        static void Print(string s, ConversionResult r) =>
            Console.WriteLine($"  {s}  =>  {r.Text}   ({r.Source.DisplayName} -> {r.Target.DisplayName})");
    }
}
