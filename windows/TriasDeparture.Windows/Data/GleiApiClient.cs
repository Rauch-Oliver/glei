using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Windows.Data;

/// <summary>The API answered 429; try again after <see cref="RetryAfter"/>.</summary>
public sealed class RateLimitedException(TimeSpan retryAfter) : Exception($"Rate limited, retry after {retryAfter}")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

public interface IGleiApi
{
    Task<IReadOnlyList<StopDto>> SearchStopsAsync(string query, CancellationToken ct = default);

    Task<IReadOnlyList<DepartureDto>> GetDeparturesAsync(
        string stopId, string? line = null, string? direction = null, int? limit = null, CancellationToken ct = default);
}

/// <summary>Talks to the glei API (never to TRIAS directly).</summary>
public sealed class GleiApiClient(HttpClient http) : IGleiApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<StopDto>> SearchStopsAsync(string query, CancellationToken ct = default) =>
        await GetAsync<List<StopDto>>($"api/stops/search?query={Uri.EscapeDataString(query)}", ct);

    public async Task<IReadOnlyList<DepartureDto>> GetDeparturesAsync(
        string stopId, string? line = null, string? direction = null, int? limit = null, CancellationToken ct = default) =>
        await GetAsync<List<DepartureDto>>(DeparturesPath(stopId, line, direction, limit), ct);

    internal static string DeparturesPath(string stopId, string? line, string? direction, int? limit)
    {
        var query = new List<string>();
        if (line is not null)      query.Add($"line={Uri.EscapeDataString(line)}");
        if (direction is not null) query.Add($"direction={Uri.EscapeDataString(direction)}");
        if (limit is not null)     query.Add($"limit={limit}");
        var path = $"api/departures/{Uri.EscapeDataString(stopId)}";
        return query.Count == 0 ? path : $"{path}?{string.Join('&', query)}";
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct) where T : new()
    {
        using var response = await http.GetAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            throw new RateLimitedException(response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? new T();
    }
}
