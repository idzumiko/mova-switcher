namespace MovaSwitcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest")
            return SelfTest.Run(args.Skip(1).ToArray());

        using var mutex = new Mutex(true, @"Local\MovaSwitcher.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("Mova Switcher is already running — look for its icon in the notification area.",
                "Mova Switcher", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
        return 0;
    }
}
