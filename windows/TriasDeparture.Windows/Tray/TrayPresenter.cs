using TriasDeparture.Domain.Models;
using TriasDeparture.Windows.Data;
using TriasDeparture.Windows.Departures;

namespace TriasDeparture.Windows.Tray;

/// <summary>State of the favorite as last fetched.</summary>
/// <param name="FetchedAtUtc">Time of the last successful fetch (null = never).</param>
/// <param name="Offline">The last fetch failed.</param>
public sealed record FavoriteSnapshot(IReadOnlyList<DepartureDto> Departures, DateTime? FetchedAtUtc, bool Offline)
{
    public static readonly FavoriteSnapshot Empty = new([], null, false);
}

/// <summary>What the tray icon shows.</summary>
/// <param name="Text">Minutes ("4"), "h" (≥ 60 min), "–" (none), "!" (offline without data), "…" (loading); null = app icon.</param>
public sealed record TrayView(string? Text, bool Dimmed, bool RealtimeDot, string ToolTip);

public static class TrayPresenter
{
    /// <summary>Windows cuts tray tooltips at 127 characters.</summary>
    public const int MaxToolTip = 127;

    public static TrayView Present(Favorite? favorite, FavoriteSnapshot snapshot, DateTime nowUtc)
    {
        if (favorite is null)
            return new TrayView(null, false, false, "glei – klick, um deine Haltestelle festzulegen");

        var title = $"{Format.StopName(favorite.StopName)} · {Format.LineName(favorite.Line)} → {favorite.DirectionLabel}";
        var next  = DepartureBoard.Next(snapshot.Departures, nowUtc);

        if (snapshot.FetchedAtUtc is not { } fetchedAt)
            return snapshot.Offline
                ? new TrayView("!", false, false, Trim($"{title}\nKeine Verbindung"))
                : new TrayView("…", true, false, Trim($"{title}\nLädt …"));

        var stand = snapshot.Offline ? $"offline · Stand {Format.Clock(fetchedAt)}" : $"Stand {Format.Clock(fetchedAt)}";

        if (next.Count == 0)
            return new TrayView("–", snapshot.Offline, false, Trim($"{title}\nKeine Abfahrten in den nächsten Stunden\n{stand}"));

        var first   = next[0];
        var minutes = first.MinutesUntilDeparture(nowUtc);
        var text    = minutes >= 60 ? "h" : minutes.ToString();
        var times   = string.Join(" · ", next.Select(d => Format.Minutes(d, nowUtc)));

        return new TrayView(text, snapshot.Offline, first.EstimatedTime is not null, Trim($"{title}\n{times}\n{stand}"));
    }

    private static string Trim(string text) => text.Length <= MaxToolTip ? text : text[..(MaxToolTip - 1)] + "…";
}
