using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace TriasDeparture.Api;

/// <summary>Per-client limit: a token bucket per IP address.</summary>
/// <remarks>
/// Generous on purpose: a client that refreshes every minute (tray app, widget) plus a stop search
/// stays far below it. Only scripts hammering the API get 429.
/// </remarks>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimiting";

    public bool Enabled { get; init; } = true;

    /// <summary>Requests a client can send in a burst.</summary>
    public int Burst { get; init; } = 60;

    /// <summary>Sustained requests per minute.</summary>
    public int PerMinute { get; init; } = 30;
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddClientRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.Section).Get<RateLimitOptions>() ?? new();

        // Cloud Run puts the client IP into X-Forwarded-For; the last entry is the one Google's front end
        // appended, earlier ones can be forged by the client.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            o.ForwardLimit = 1;
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                return ValueTask.CompletedTask;
            };

            if (!options.Enabled)
                return;

            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
                RateLimitPartition.GetTokenBucketLimiter(
                    http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit          = options.Burst,
                        TokensPerPeriod     = options.PerMinute,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit          = 0,
                        AutoReplenishment   = true
                    }));
        });

        return services;
    }
}
