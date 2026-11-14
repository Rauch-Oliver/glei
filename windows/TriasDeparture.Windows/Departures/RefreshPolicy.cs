namespace TriasDeparture.Windows.Departures;

/// <summary>When to fetch the favorite again.</summary>
public static class RefreshPolicy
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(5)];

    /// <summary>Wait after the n-th failure in a row (1-based): 1, 2, then 5 minutes.</summary>
    public static TimeSpan AfterError(int consecutiveErrors) =>
        Backoff[Math.Clamp(consecutiveErrors, 1, Backoff.Length) - 1];

    /// <summary>
    /// Fetch early when fewer than <paramref name="wanted"/> trips are left although the last answer had more –
    /// the gone ones are replaced right away. Not when the API itself had fewer (e.g. at night), or that would loop.
    /// </summary>
    public static bool NeedsRefill(int remaining, int lastFetchedCount, int wanted = 3) =>
        remaining < wanted && lastFetchedCount > remaining;
}
