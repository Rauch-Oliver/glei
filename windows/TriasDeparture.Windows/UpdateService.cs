using Serilog;
using Velopack;
using Velopack.Sources;

namespace TriasDeparture.Windows;

/// <summary>
/// Checks GitHub Releases at startup and every 24 h; a found update is downloaded in the background
/// and applied when the app exits (next start runs the new version).
/// </summary>
public sealed class UpdateService : IDisposable
{
    private readonly UpdateManager _manager = new(new GithubSource(AppInfo.RepositoryUrl, accessToken: null, prerelease: false));
    private readonly CancellationTokenSource _stop = new();

    /// <summary>False when running from a build folder (development) – then nothing is checked.</summary>
    public bool IsInstalled => _manager.IsInstalled;

    public void Start()
    {
        if (!IsInstalled)
            return;
        _ = LoopAsync(_stop.Token);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            while (!ct.IsCancellationRequested)
            {
                await CheckAsync();
                await Task.Delay(TimeSpan.FromHours(24), ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task CheckAsync()
    {
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
                return;

            Log.Information("Downloading update {Version}", update.TargetFullRelease.Version);
            await _manager.DownloadUpdatesAsync(update);
            _manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Update check failed");
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
