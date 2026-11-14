using System.Net;
using System.Net.Http;
using System.Text;
using Shouldly;
using TriasDeparture.Windows.Data;

namespace TriasDeparture.Windows.Tests;

public sealed class DataTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "glei-tests-" + Guid.NewGuid().ToString("N"));

    private sealed class StubHandler(HttpStatusCode status, string body, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter is { } delta)
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
            return Task.FromResult(response);
        }
    }

    private static GleiApiClient Client(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://glei.example/") });

    // ── API client ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Departures_ParsesApiJson_AndSendsFilters()
    {
        const string json = """
            [{"line":"23","destination":"Jakominiplatz","minutes":14,"plannedTime":"2026-11-14T19:38:00Z",
              "estimatedTime":"2026-11-14T19:39:00Z","mode":"tram","cancelled":false,"direction":"inward"}]
            """;
        var handler = new StubHandler(HttpStatusCode.OK, json);

        var departures = await Client(handler).GetDeparturesAsync("at:46:3171", "23", "inward", 10);

        handler.LastUri!.PathAndQuery.ShouldBe("/api/departures/at%3A46%3A3171?line=23&direction=inward&limit=10");
        var d = departures.ShouldHaveSingleItem();
        d.Line.ShouldBe("23");
        d.Direction.ShouldBe("inward");
        d.EstimatedTime.ShouldBe(new DateTime(2026, 11, 14, 19, 39, 0, DateTimeKind.Utc));
        d.EstimatedTime!.Value.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public async Task TooManyRequests_ThrowsWithRetryAfter()
    {
        var handler = new StubHandler(HttpStatusCode.TooManyRequests, "", TimeSpan.FromSeconds(42));

        var ex = await Should.ThrowAsync<RateLimitedException>(() => Client(handler).SearchStopsAsync("krenn"));

        ex.RetryAfter.ShouldBe(TimeSpan.FromSeconds(42));
    }

    [Fact]
    public async Task Search_EscapesQuery()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """[{"id":"at:46:1","name":"Graz Hauptplatz"}]""");

        var stops = await Client(handler).SearchStopsAsync("Süd tirol");

        handler.LastUri!.Query.ShouldBe("?query=S%C3%BCd%20tirol");
        stops.ShouldHaveSingleItem().Name.ShouldBe("Graz Hauptplatz");
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    [Fact]
    public void RecentStops_MostRecentFirst_NoDuplicates_AtMostFive()
    {
        var settings = new AppSettings();
        foreach (var i in Enumerable.Range(1, 7))
            settings.AddRecent(new RecentStop($"s{i}", $"Stop {i}"));
        settings.AddRecent(new RecentStop("s5", "Stop 5"));

        settings.RecentStops.Select(s => s.StopId).ShouldBe(["s5", "s7", "s6", "s4", "s3"]);
    }

    [Fact]
    public void SettingsStore_RoundTrip()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var settings = new AppSettings
        {
            Favorite = new Favorite("at:46:3171", "Graz Krenngasse", "23", "inward", "Jakominiplatz"),
            StartWithWindows = false
        };

        store.Save(settings);
        var loaded = store.Load();

        loaded.Favorite.ShouldBe(settings.Favorite);
        loaded.StartWithWindows.ShouldBeFalse();
    }

    [Fact]
    public void SettingsStore_BrokenFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ not json");

        var loaded = new SettingsStore(path).Load();

        loaded.Favorite.ShouldBeNull();
        loaded.StartWithWindows.ShouldBeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
