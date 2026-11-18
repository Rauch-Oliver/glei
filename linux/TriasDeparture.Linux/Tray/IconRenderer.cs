using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace TriasDeparture.Linux.Tray;

/// <summary>
/// Draws the minutes as tray icon. Tray hosts (StatusNotifierItem) scale the pixmap to the panel size,
/// so it is rendered once in a generous size.
/// </summary>
public static class IconRenderer
{
    public const int IconSize = 64;
    private static readonly Color RealtimeColor = Color.FromRgb(0xFB, 0xBF, 0x24);

    /// <summary>
    /// True if the panel is light (then the number is drawn black). GNOME's top bar is dark in both themes;
    /// other desktops (KDE, Xfce, Cinnamon …) follow the system theme.
    /// </summary>
    public static bool IsPanelLight(string? currentDesktop, ThemeVariant? systemTheme) =>
        !IsGnome(currentDesktop) && systemTheme == ThemeVariant.Light;

    public static bool IsGnome(string? currentDesktop) =>
        (currentDesktop ?? "").Split(':').Any(d => d.Equals("GNOME", StringComparison.OrdinalIgnoreCase));

    public static WindowIcon Render(string text, bool lightPanel, bool dimmed, bool realtimeDot)
    {
        const int size = IconSize;
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        using (var ctx = bitmap.CreateDrawingContext())
        {
            var color = lightPanel ? Colors.Black : Colors.White;
            if (dimmed) color = Color.FromArgb(130, color.R, color.G, color.B);
            var brush = new SolidColorBrush(color);
            var typeface = new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

            // As big as possible: one digit fills the icon, two digits are squeezed to fit the width
            var em = size * (text.Length > 1 ? 0.92 : 1.05);
            FormattedText formatted;
            while (true)
            {
                formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, em, brush);
                if (formatted.WidthIncludingTrailingWhitespace <= size * 1.02 || em < 6)
                    break;
                em -= 1;
            }

            // Center on the ink of the glyphs, not on the line box (digits have no descender)
            var ink = formatted.BuildGeometry(new Point(0, 0))?.Bounds ?? new Rect(0, 0, formatted.Width, formatted.Height);
            var origin = new Point((size - ink.Width) / 2 - ink.X, (size - ink.Height) / 2 - ink.Y);
            ctx.DrawText(formatted, origin);

            if (realtimeDot)
            {
                var d = size / 4.0;
                ctx.DrawEllipse(new SolidColorBrush(RealtimeColor), null, new Rect(size - d, size - d, d, d));
            }
        }

        using var png = new MemoryStream();
        bitmap.Save(png, new PngBitmapEncoderOptions());
        png.Position = 0;
        return new WindowIcon(png);
    }

    /// <summary>The glei app icon (shown while no favorite is set).</summary>
    public static WindowIcon AppIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://glei/Assets/glei.ico"));
        return new WindowIcon(stream);
    }
}
