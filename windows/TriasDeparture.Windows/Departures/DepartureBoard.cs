using System.Globalization;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Windows.Departures;

/// <summary>Departures of one line in one direction at a stop.</summary>
/// <param name="Label">Most frequent destination of the group, e.g. "Andritz".</param>
/// <param name="Rows">The next departures; cancelled ones are included but don't count.</param>
public sealed record DepartureGroup(string Line, string? Direction, string Label, IReadOnlyList<DepartureDto> Rows);

/// <summary>Pure display logic: which departures to show at a given time. Minutes are always computed locally.</summary>
public static class DepartureBoard
{
    /// <summary>A departure stays visible for this long after its time ("jetzt").</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    /// <summary>Departures not yet gone, by time.</summary>
    public static IReadOnlyList<DepartureDto> Upcoming(IEnumerable<DepartureDto> departures, DateTime nowUtc) =>
        departures.Where(d => d.EffectiveTime >= nowUtc - Grace).OrderBy(d => d.EffectiveTime).ToList();

    /// <summary>
    /// Rows to display until <paramref name="count"/> running trips are shown; cancelled trips
    /// in between are listed too (struck through) but don't count.
    /// </summary>
    public static IReadOnlyList<DepartureDto> Rows(IEnumerable<DepartureDto> departures, DateTime nowUtc, int count = 3)
    {
        var rows = new List<DepartureDto>();
        var running = 0;
        foreach (var d in Upcoming(departures, nowUtc))
        {
            if (running == count) break;
            rows.Add(d);
            if (!d.Cancelled) running++;
        }
        return rows;
    }

    /// <summary>The next running trips only.</summary>
    public static IReadOnlyList<DepartureDto> Next(IEnumerable<DepartureDto> departures, DateTime nowUtc, int count = 3) =>
        Upcoming(departures, nowUtc).Where(d => !d.Cancelled).Take(count).ToList();

    /// <summary>All departures of a stop grouped by line and direction, lines in natural order (3, 4, 23, 64E, N5).</summary>
    public static IReadOnlyList<DepartureGroup> Group(IEnumerable<DepartureDto> departures, DateTime nowUtc, int perGroup = 3) =>
        Upcoming(departures, nowUtc)
            .GroupBy(d => (d.Line, Key: d.Direction ?? d.Destination))
            .Select(g => new DepartureGroup(
                g.Key.Line,
                g.First().Direction,
                g.GroupBy(d => d.Destination).OrderByDescending(x => x.Count()).ThenBy(x => x.Min(d => d.EffectiveTime)).First().Key,
                Rows(g, nowUtc, perGroup)))
            .Where(g => g.Rows.Count > 0)
            .OrderBy(g => LineSortKey(g.Line))
            .ThenBy(g => g.Line, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Label, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Numeric lines first by number, then lettered ones ("64E" after 64, "N5" after all numbers).</summary>
    private static (int, int, string) LineSortKey(string line)
    {
        var digits = new string(line.TakeWhile(char.IsAsciiDigit).ToArray());
        return digits.Length > 0
            ? (0, int.Parse(digits, CultureInfo.InvariantCulture), line[digits.Length..])
            : (1, 0, line);
    }
}
