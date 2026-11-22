using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MovaSwitcher;

/// <summary>Draws the app icon at runtime (a rounded badge reading "Aя"), so no .ico file is needed.</summary>
internal static class TrayIcon
{
    public static Icon Create(bool enabled = true)
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var color = enabled ? Color.FromArgb(0, 91, 187) : Color.FromArgb(128, 128, 128);
            using var path = RoundedRect(new Rectangle(0, 0, size - 1, size - 1), 7);
            using var fill = new SolidBrush(color);
            g.FillPath(fill, path);

            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(enabled ? Color.FromArgb(255, 213, 0) : Color.White);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Aя", font, text, new RectangleF(0, 1, size, size), format);
        }

        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        Native.DestroyIcon(hIcon);
        return icon;
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
