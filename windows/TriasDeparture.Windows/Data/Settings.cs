using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace TriasDeparture.Windows.Data;

/// <summary>The one favorite: a stop, a line and one of its two directions.</summary>
/// <param name="Direction">"outward" or "inward" (TRIAS DirectionRef).</param>
/// <param name="DirectionLabel">Destination shown to the user, e.g. "Andritz".</param>
public sealed record Favorite(string StopId, string StopName, string Line, string Direction, string DirectionLabel);

public sealed record RecentStop(string StopId, string StopName);

public sealed class AppSettings
{
    public Favorite? Favorite { get; set; }
    public List<RecentStop> RecentStops { get; set; } = [];
    public bool StartWithWindows { get; set; } = true;
    public bool WelcomeShown { get; set; }

    public const int MaxRecentStops = 5;

    /// <summary>Moves <paramref name="stop"/> to the front of the recent stops, keeping at most 5.</summary>
    public void AddRecent(RecentStop stop)
    {
        RecentStops.RemoveAll(s => s.StopId == stop.StopId);
        RecentStops.Insert(0, stop);
        if (RecentStops.Count > MaxRecentStops)
            RecentStops.RemoveRange(MaxRecentStops, RecentStops.Count - MaxRecentStops);
    }
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON; a broken file falls back to defaults.</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AppSettings Load()
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Settings unreadable, using defaults");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Saving settings failed");
        }
    }
}
