using Avalonia;
using Avalonia.Styling;
using Shouldly;
using TriasDeparture.Linux.Data;
using TriasDeparture.Linux.Popup;
using TriasDeparture.Linux.Tray;

namespace TriasDeparture.Linux.Tests;

public sealed class LinuxTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "glei-linux-tests-" + Guid.NewGuid().ToString("N"));

    public LinuxTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // ── Desktop integration ─────────────────────────────────────────────────

    private DesktopIntegration Desktop(string exe = "/home/me/Apps/glei.AppImage") =>
        new(Path.Combine(_dir, "config"), Path.Combine(_dir, "data"), exe);

    [Fact]
    public void Autostart_WritesAndRemovesTheDesktopFile()
    {
        var desktop = Desktop();

        desktop.ApplyAutostart(true);
        desktop.IsAutostartEnabled.ShouldBeTrue();
        File.ReadAllText(desktop.AutostartFile).ShouldContain("Exec=/home/me/Apps/glei.AppImage\n");
        desktop.AutostartFile.ShouldEndWith(Path.Combine("config", "autostart", "glei.desktop"));

        desktop.ApplyAutostart(false);
        desktop.IsAutostartEnabled.ShouldBeFalse();
        desktop.ApplyAutostart(false); // already gone: no error
    }

    [Fact]
    public void Entry_IsAValidDesktopEntry_HiddenInTheMenuOnlyForAutostart()
    {
        var desktop = Desktop();

        var menu = desktop.Entry(autostart: false);
        menu.ShouldStartWith("[Desktop Entry]\n");
        menu.ShouldContain("Type=Application\n");
        menu.ShouldContain("Icon=glei\n");
        menu.ShouldNotContain("NoDisplay");

        desktop.Entry(autostart: true).ShouldContain("NoDisplay=true\n");
    }

    [Theory]
    [InlineData("/opt/glei/glei", "/opt/glei/glei")]
    [InlineData("/home/me/My Apps/glei.AppImage", "\"/home/me/My Apps/glei.AppImage\"")]
    [InlineData("/home/me/$x/glei", "\"/home/me/\\\\$x/glei\"")]
    public void QuoteExec_QuotesReservedCharacters(string path, string expected) =>
        DesktopIntegration.QuoteExec(path).ShouldBe(expected);

    // ── Tray icon color ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ubuntu:GNOME", "Light", false)]  // GNOME's top bar is always dark
    [InlineData("GNOME", "Dark", false)]
    [InlineData("KDE", "Light", true)]
    [InlineData("KDE", "Dark", false)]
    [InlineData(null, "Light", true)]
    public void PanelColor_FollowsTheDesktop(string? desktop, string theme, bool light) =>
        IconRenderer.IsPanelLight(desktop, theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark).ShouldBe(light);

    // ── Popup placement ─────────────────────────────────────────────────────

    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);
    private static readonly PixelSize Popup = new(360, 480);

    [Fact]
    public void Placement_TopPanel_TopRightBelowIt()
    {
        var pos = PopupPlacement.Place(Screen, new PixelRect(0, 32, 1920, 1048), Popup, gnome: false);
        pos.ShouldBe(new PixelPoint(1920 - 360 - 12, 32 + 12));
    }

    [Fact]
    public void Placement_BottomPanel_BottomRightAboveIt()
    {
        var pos = PopupPlacement.Place(Screen, new PixelRect(0, 0, 1920, 1036), Popup, gnome: false);
        pos.ShouldBe(new PixelPoint(1920 - 360 - 12, 1036 - 480 - 12));
    }

    [Theory]
    [InlineData(true, 12)]           // GNOME: tray is in the top bar
    [InlineData(false, 1080 - 480 - 12)]
    public void Placement_UnknownPanels_GuessFromDesktop(bool gnome, int y) =>
        PopupPlacement.Place(Screen, Screen, Popup, gnome).Y.ShouldBe(y);

    [Fact]
    public void Placement_ScalesTheGap()
    {
        var pos = PopupPlacement.Place(Screen, Screen, Popup, gnome: true, scaling: 2);
        pos.ShouldBe(new PixelPoint(1920 - 360 - 24, 24));
    }

    // ── Single instance ─────────────────────────────────────────────────────

    [Fact]
    public async Task SingleInstance_SecondStartSignalsTheFirst()
    {
        using var first = SingleInstance.Acquire("glei-test", _dir);
        first.IsFirst.ShouldBeTrue();

        var signaled = new TaskCompletionSource();
        first.Listen(() => signaled.TrySetResult());

        using (var second = SingleInstance.Acquire("glei-test", _dir))
        {
            second.IsFirst.ShouldBeFalse();
            second.SignalFirst();
        }

        await signaled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SingleInstance_LockIsReleasedOnDispose()
    {
        SingleInstance.Acquire("glei-test", _dir).Dispose();
        using var again = SingleInstance.Acquire("glei-test", _dir);
        again.IsFirst.ShouldBeTrue();
    }
}
