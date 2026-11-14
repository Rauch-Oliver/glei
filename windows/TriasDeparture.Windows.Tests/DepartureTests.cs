using Shouldly;
using TriasDeparture.Domain.Models;
using TriasDeparture.Windows.Data;
using TriasDeparture.Windows.Departures;
using TriasDeparture.Windows.Tray;

namespace TriasDeparture.Windows.Tests;

public sealed class DepartureTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 15, 0, 0, DateTimeKind.Utc);

    private static DepartureDto Dep(string line, double minutes, string destination = "Andritz", string? direction = "outward",
        bool realtime = true, bool cancelled = false) =>
        new(line, destination, 0, Now.AddMinutes(minutes), realtime ? Now.AddMinutes(minutes) : null, "tram", cancelled, direction);

    private static readonly Favorite Krenngasse = new("at:46:3171", "Graz Krenngasse", "23", "inward", "Jakominiplatz");

    // ── DepartureBoard ───────────────────────────────────────────────────────

    [Fact]
    public void Rows_DropGoneTrips_AndDontCountCancelledOnes()
    {
        var departures = new[] { Dep("3", -2), Dep("3", 4), Dep("3", 8, cancelled: true), Dep("3", 11), Dep("3", 19), Dep("3", 25) };

        var rows = DepartureBoard.Rows(departures, Now);

        rows.Select(r => (int)(r.EffectiveTime - Now).TotalMinutes).ShouldBe([4, 8, 11, 19]);
        rows.Count(r => !r.Cancelled).ShouldBe(3);
    }

    [Fact]
    public void Rows_KeepADepartureForHalfAMinute_AsJetzt()
    {
        var rows = DepartureBoard.Rows([Dep("3", -0.4), Dep("3", 5)], Now);

        rows.Count.ShouldBe(2);
        Format.Minutes(rows[0], Now).ShouldBe("jetzt");
    }

    [Fact]
    public void Group_ByLineAndDirection_LabelIsMostFrequentDestination_LinesInNaturalOrder()
    {
        var departures = new[]
        {
            Dep("23", 2, "Steyrergasse", "inward"), Dep("23", 5, "Jakominiplatz", "inward"), Dep("23", 9, "Jakominiplatz", "inward"),
            Dep("N5", 1, "Puntigam", "outward"), Dep("4", 3, "Liebenau", "outward"), Dep("64E", 6, "Eggenberg", "outward"),
            Dep("64", 7, "Eggenberg", "outward")
        };

        var groups = DepartureBoard.Group(departures, Now);

        groups.Select(g => g.Line).ShouldBe(["4", "23", "64", "64E", "N5"]);
        groups.Single(g => g.Line == "23").Label.ShouldBe("Jakominiplatz");
        groups.Single(g => g.Line == "23").Rows.Count.ShouldBe(3);
    }

    // ── Format ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("3", "3er")]
    [InlineData("23", "23er")]
    [InlineData("N5", "Linie N5")]
    [InlineData("64E", "Linie 64E")]
    public void LineName(string line, string expected) => Format.LineName(line).ShouldBe(expected);

    [Fact]
    public void MinutesList_SkipsCancelled_AndSaysJetzt() =>
        Format.MinutesList([Dep("3", 0), Dep("3", 4, cancelled: true), Dep("3", 11)], Now).ShouldBe("jetzt · 11 min");

    [Theory]
    [InlineData("Graz Krenngasse", "Krenngasse")]
    [InlineData("Wien Krenngasse", "Wien Krenngasse")]
    [InlineData("Graz", "Graz")]
    public void StopName_DropsGrazPrefix(string name, string expected) => Format.StopName(name).ShouldBe(expected);

    // ── RefreshPolicy ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 5)]
    [InlineData(9, 5)]
    public void Backoff(int errors, int minutes) => RefreshPolicy.AfterError(errors).ShouldBe(TimeSpan.FromMinutes(minutes));

    [Theory]
    [InlineData(2, 10, true)]   // trips left the board, more were fetched → refill
    [InlineData(2, 2, false)]   // the API itself had only two (night) → don't loop
    [InlineData(3, 10, false)]
    public void NeedsRefill(int remaining, int fetched, bool expected) =>
        RefreshPolicy.NeedsRefill(remaining, fetched).ShouldBe(expected);

    // ── TrayPresenter ────────────────────────────────────────────────────────

    [Fact]
    public void Tray_NoFavorite_ShowsAppIcon() =>
        TrayPresenter.Present(null, FavoriteSnapshot.Empty, Now).Text.ShouldBeNull();

    [Fact]
    public void Tray_Loading() =>
        TrayPresenter.Present(Krenngasse, FavoriteSnapshot.Empty, Now).Text.ShouldBe("…");

    [Fact]
    public void Tray_OfflineWithoutData_ShowsExclamation() =>
        TrayPresenter.Present(Krenngasse, FavoriteSnapshot.Empty with { Offline = true }, Now).Text.ShouldBe("!");

    [Fact]
    public void Tray_ShowsMinutes_RealtimeDot_AndTooltip()
    {
        var snapshot = new FavoriteSnapshot([Dep("23", 4), Dep("23", 11, realtime: false), Dep("23", 19)], Now, false);

        var view = TrayPresenter.Present(Krenngasse, snapshot, Now);

        view.Text.ShouldBe("4");
        view.RealtimeDot.ShouldBeTrue();
        view.Dimmed.ShouldBeFalse();
        view.ToolTip.ShouldStartWith("Krenngasse · 23er → Jakominiplatz\n4 min · 11 min · 19 min\nStand ");
        view.ToolTip.Length.ShouldBeLessThanOrEqualTo(TrayPresenter.MaxToolTip);
    }

    [Fact]
    public void Tray_Offline_DimsLastNumber() =>
        TrayPresenter.Present(Krenngasse, new FavoriteSnapshot([Dep("23", 7)], Now.AddMinutes(-3), true), Now)
            .ShouldSatisfyAllConditions(v => v.Text.ShouldBe("7"), v => v.Dimmed.ShouldBeTrue(), v => v.ToolTip.ShouldContain("offline"));

    [Fact]
    public void Tray_AnHourOrMore_ShowsH() =>
        TrayPresenter.Present(Krenngasse, new FavoriteSnapshot([Dep("23", 75)], Now, false), Now).Text.ShouldBe("h");

    [Fact]
    public void Tray_NoDepartures_ShowsDash() =>
        TrayPresenter.Present(Krenngasse, new FavoriteSnapshot([], Now, false), Now).Text.ShouldBe("–");
}
