using System.Runtime.InteropServices;

namespace MovaSwitcher;

/// <summary>
/// Clipboard access with retries (another app may briefly hold the clipboard open),
/// plus snapshot/restore so converting text doesn't lose what the user had copied.
/// </summary>
internal static class ClipboardHelper
{
    public sealed class Snapshot
    {
        internal readonly Dictionary<string, object> Formats = new();
    }

    public static Snapshot? Save()
    {
        return Retry(() =>
        {
            var data = Clipboard.GetDataObject();
            if (data == null) return null;
            var snap = new Snapshot();
            foreach (var format in data.GetFormats(autoConvert: false))
            {
                try
                {
                    var value = data.GetData(format, autoConvert: false);
                    if (value != null) snap.Formats[format] = value;
                }
                catch
                {
                    // Some private formats can't be read back; skip them.
                }
            }
            return snap;
        });
    }

    public static void Restore(Snapshot? snap)
    {
        Retry(() =>
        {
            if (snap == null || snap.Formats.Count == 0)
            {
                Clipboard.Clear();
                return true;
            }
            var data = new DataObject();
            foreach (var (format, value) in snap.Formats)
            {
                try { data.SetData(format, value); } catch { }
            }
            Clipboard.SetDataObject(data, copy: true);
            return true;
        });
    }

    /// <summary>Put text on the clipboard, asking Windows not to keep it in clipboard history (Win+V).</summary>
    public static bool SetText(string text)
    {
        return Retry(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4]));
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4]));
            Clipboard.SetDataObject(data, copy: true);
            return true;
        });
    }

    /// <summary>Wait for the clipboard to change after a Ctrl+C and return its text.</summary>
    public static async Task<string?> WaitForNewTextAsync(uint sequenceBefore, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Native.GetClipboardSequenceNumber() == sequenceBefore)
        {
            if (Environment.TickCount64 > deadline) return null;
            await Task.Delay(10);
        }
        // Some apps put several formats on the clipboard one after another; give them a moment.
        await Task.Delay(30);
        return Retry(() => Clipboard.ContainsText() ? Clipboard.GetText() : null);
    }

    private static T? Retry<T>(Func<T?> action)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                return action();
            }
            catch (ExternalException)
            {
                Thread.Sleep(25);
            }
        }
        return default;
    }
}
