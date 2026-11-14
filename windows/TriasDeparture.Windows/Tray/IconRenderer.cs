using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace TriasDeparture.Windows.Tray;

/// <summary>Draws the minutes as tray icon at the size Windows uses for the current DPI.</summary>
public static partial class IconRenderer
{
    private const int SmallIconMetric = 49; // SM_CXSMICON
    private static readonly Color RealtimeColor = Color.FromArgb(0xFB, 0xBF, 0x24);

    public static int IconSize => GetSystemMetricsForDpi(SmallIconMetric, GetDpiForSystem());

    /// <summary>True if the taskbar is light (then the number is drawn black).</summary>
    public static bool IsTaskbarLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }

    public static Icon Render(string text, bool lightTaskbar, bool dimmed, bool realtimeDot)
    {
        var size = IconSize;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            var color = lightTaskbar ? Color.Black : Color.White;
            if (dimmed) color = Color.FromArgb(130, color);

            // As big as possible: one digit fills the icon, two digits are squeezed to fit the width
            using var format = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            var em = size * (text.Length > 1 ? 0.92f : 1.05f);
            Font font;
            while (true)
            {
                font = new Font("Segoe UI", em, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
                if (g.MeasureString(text, font, size * 4, format).Width <= size * 1.02f || em < 6)
                    break;
                font.Dispose();
                em -= 0.5f;
            }

            using (font)
            using (var brush = new SolidBrush(color))
            {
                // Segoe UI sits low in its line box – lift it a little
                g.DrawString(text, font, brush, new RectangleF(0, -size * 0.06f, size, size), format);
            }

            if (realtimeDot)
            {
                var d = Math.Max(3, size / 4f);
                using var dot = new SolidBrush(RealtimeColor);
                g.FillEllipse(dot, size - d, size - d, d, d);
            }
        }

        return ToIcon(bitmap);
    }

    /// <summary>The glei app icon at tray size (shown while no favorite is set).</summary>
    public static Icon AppIcon()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/glei.ico"))!.Stream;
        using (stream)
            return new Icon(stream, IconSize, IconSize);
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(handle);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetricsForDpi(int index, uint dpi);

    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForSystem();
}
