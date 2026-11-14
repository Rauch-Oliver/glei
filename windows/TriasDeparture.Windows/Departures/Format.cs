using System.Globalization;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Windows.Departures;

/// <summary>German display texts.</summary>
public static class Format
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-AT");

    /// <summary>"3er" for numeric lines (how Graz says it), "Linie N5" otherwise.</summary>
    public static string LineName(string line) =>
        line.All(char.IsAsciiDigit) ? $"{line}er" : $"Linie {line}";

    /// <summary>"jetzt" or "4 min".</summary>
    public static string Minutes(int minutes) => minutes == 0 ? "jetzt" : $"{minutes} min";

    public static string Minutes(DepartureDto d, DateTime nowUtc) => Minutes(d.MinutesUntilDeparture(nowUtc));

    /// <summary>"4 · 11 · 19 min" for a compact list.</summary>
    public static string MinutesList(IEnumerable<DepartureDto> departures, DateTime nowUtc)
    {
        var minutes = departures.Where(d => !d.Cancelled).Select(d => d.MinutesUntilDeparture(nowUtc)).ToList();
        return minutes.Count switch
        {
            0 => "fällt aus",
            1 => Minutes(minutes[0]),
            _ when minutes[0] == 0 => "jetzt · " + string.Join(" · ", minutes.Skip(1)) + " min",
            _ => string.Join(" · ", minutes) + " min"
        };
    }

    /// <summary>"Graz Krenngasse" → "Krenngasse": inside Graz the city prefix is noise.</summary>
    public static string StopName(string name) =>
        name.StartsWith("Graz ", StringComparison.Ordinal) && name.Length > 5 ? name[5..] : name;

    /// <summary>"16:04" in local time.</summary>
    public static string Clock(DateTime utc) => utc.ToLocalTime().ToString("HH:mm", German);
}
