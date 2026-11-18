using Avalonia;

namespace TriasDeparture.Linux.Popup;

/// <summary>Where the popup goes: in the corner next to the panel that holds the tray, like the desktop's own menus.</summary>
public static class PopupPlacement
{
    public const int EdgeGap = 12;

    /// <param name="bounds">Screen in pixels.</param>
    /// <param name="workingArea">Screen minus panels in pixels (equal to <paramref name="bounds"/> when unknown, e.g. on Wayland).</param>
    /// <param name="size">Popup size in pixels.</param>
    /// <param name="gnome">GNOME: tray in the top bar, so top right when the panels are unknown.</param>
    public static PixelPoint Place(PixelRect bounds, PixelRect workingArea, PixelSize size, bool gnome, double scaling = 1)
    {
        var gap    = (int)Math.Round(EdgeGap * scaling);
        var top    = workingArea.Y - bounds.Y;
        var bottom = bounds.Bottom - workingArea.Bottom;
        var atTop  = top > bottom || (top == bottom && gnome);

        var x = workingArea.Right - size.Width - gap;
        var y = atTop ? workingArea.Y + gap : workingArea.Bottom - size.Height - gap;
        return new PixelPoint(Math.Max(workingArea.X, x), Math.Max(workingArea.Y, y));
    }
}
