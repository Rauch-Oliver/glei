using TriasDeparture.Application.Interfaces;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Application.Services;

/// <summary>Optional filters for departures. Null properties don't filter.</summary>
/// <param name="Line">Line name as TRIAS knows it, e.g. "3" or "N5" (case-insensitive).</param>
/// <param name="Direction">"outward" or "inward" (case-insensitive).</param>
/// <param name="Limit">Maximum number of departures returned.</param>
public sealed record DepartureFilter(string? Line = null, string? Direction = null, int? Limit = null);

/// <summary>Orchestrates stop search and departure fetching.</summary>
public sealed class DepartureService
{
    private readonly ITriasClient _client;

    public DepartureService(ITriasClient client) => _client = client;

    // ────────────────────────────────────────────────────────────────
    // Stop search
    // ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<StopDto>> SearchStopsAsync(string query, CancellationToken ct = default)
        => _client.SearchStopsAsync(query, ct);

    // ────────────────────────────────────────────────────────────────
    // Departures
    // ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<DepartureDto>> GetDeparturesAsync(string stopId, CancellationToken ct = default)
        => _client.GetDeparturesAsync(stopId, ct: ct);

    public Task<IReadOnlyList<DepartureDto>> GetDeparturesByLineAsync(
        string stopId, string line, CancellationToken ct = default)
        => GetDeparturesAsync(stopId, new DepartureFilter(Line: line), ct);

    public async Task<IReadOnlyList<DepartureDto>> GetDeparturesAsync(
        string stopId, DepartureFilter filter, CancellationToken ct = default)
    {
        IEnumerable<DepartureDto> departures = await _client.GetDeparturesAsync(stopId, ct: ct);

        if (!string.IsNullOrWhiteSpace(filter.Line))
            departures = departures.Where(d => Same(d.Line, filter.Line));
        if (!string.IsNullOrWhiteSpace(filter.Direction))
            departures = departures.Where(d => d.Direction is not null && Same(d.Direction, filter.Direction));
        if (filter.Limit is { } limit)
            departures = departures.Take(limit);

        return departures.ToList().AsReadOnly();
    }

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
}
