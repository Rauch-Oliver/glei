using System.Net.Http;
using System.Windows;
using Serilog;
using TriasDeparture.Windows.Data;
using TriasDeparture.Windows.Tray;

namespace TriasDeparture.Windows;

public partial class App : Application
{
    private readonly SingleInstance _instance;
    private TrayController? _tray;
    private UpdateService? _updates;

    public App(SingleInstance instance) => _instance = instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(AppPaths.LogDirectory, "glei-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
        Log.Information("glei {Version} starting, API {Api}", AppInfo.Version, AppConfig.ApiBaseUrl);

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };

        var http = new HttpClient { BaseAddress = AppConfig.ApiBaseUrl, Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"glei-windows/{AppInfo.Version}");

        _updates = new UpdateService();
        _tray = new TrayController(new GleiApiClient(http), new SettingsStore(AppPaths.SettingsFile), _updates.IsInstalled);
        _tray.Start();

        _instance.Listen(() => Dispatcher.BeginInvoke(() => _tray.ShowPopup()));
        _updates.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _updates?.Dispose();
        Log.Information("glei exiting");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
