using Shouldly;
using TriasDeparture.Application.Interfaces;
using TriasDeparture.Application.Services;
using TriasDeparture.Domain.Models;

namespace TriasDeparture.Tests;

public sealed class DepartureServiceTests
{
    private static readonly DateTime T = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeTriasClient : ITriasClient
    {
        public Dictionary<string, List<DepartureDto>> Departures { get; } = [];

        public Task<IReadOnlyList<StopDto>> SearchStopsAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<StopDto>>([]);

        public Task<IReadOnlyList<DepartureDto>> GetDeparturesAsync(string stopId, bool includeOnwardStops = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DepartureDto>>(Departures.GetValueOrDefault(stopId, []));
    }

    private static DepartureDto Dep(string line, int minutes, string? direction = null) =>
        new(line, "Andritz", minutes, T.AddMinutes(minutes), null, "tram", Direction: direction);

    private readonly FakeTriasClient _client = new();
    private DepartureService Service => new(_client);

    [Fact]
    public async Task GetDeparturesByLine_FiltersCaseInsensitive()
    {
        _client.Departures["x:1"] = [Dep("64E", 1), Dep("64", 2), Dep("n5", 3)];

        var result = await Service.GetDeparturesByLineAsync("x:1", "N5");

        result.Select(d => d.Line).ShouldBe(["n5"]);
    }

    [Fact]
    public async Task GetDepartures_FiltersByLineAndDirection_AndLimits()
    {
        _client.Departures["x:1"] =
        [
            Dep("3", 1, "outward"), Dep("3", 2, "inward"), Dep("3", 4, "outward"),
            Dep("4", 5, "outward"), Dep("3", 7, "outward"), Dep("3", 9, null)
        ];

        var result = await Service.GetDeparturesAsync("x:1", new DepartureFilter("3", "Outward", Limit: 2));

        result.Select(d => d.Minutes).ShouldBe([1, 4]);
    }

    [Fact]
    public async Task GetDepartures_WithoutFilter_ReturnsAll()
    {
        _client.Departures["x:1"] = [Dep("3", 1), Dep("4", 2)];

        var result = await Service.GetDeparturesAsync("x:1", new DepartureFilter());

        result.Count.ShouldBe(2);
    }
}
