using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Win32;
using Serilog;
using TriasDeparture.Windows.Data;
using TriasDeparture.Windows.Popup;

namespace TriasDeparture.Windows.Tray;

/// <summary>Wires the tray icon, its menu, the popup, the settings and the system events together.</summary>
public sealed class TrayController : IDisposable
{
    private static readonly TimeSpan ReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly IGleiApi _api;
    private readonly SettingsStore _store;
    private readonly bool _isInstalled;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly AppSettings _settings;
    private readonly FavoriteMonitor _monitor;

    private TaskbarIcon? _icon;
    private System.Drawing.Icon? _currentIcon;
    private PopupWindow? _popup;
    private PopupViewModel? _viewModel;
    private TrayView? _shown;
    private bool _lightTaskbar;

    public TrayController(IGleiApi api, SettingsStore store, bool isInstalled)
    {
        _api         = api;
        _store       = store;
        _isInstalled = isInstalled;
        _settings    = store.Load();
        _monitor     = new FavoriteMonitor(api);
    }

    public void Start()
    {
        _lightTaskbar = IconRenderer.IsTaskbarLight();
        _viewModel    = new PopupViewModel(_api, _monitor, _settings, () => _store.Save(_settings), SetFavorite);
        _popup        = new PopupWindow(_viewModel);

        _icon = new TaskbarIcon
        {
            ToolTipText      = "glei",
            NoLeftClickDelay = true,
            ContextMenu      = BuildMenu()
        };
        _icon.TrayLeftMouseUp += (_, _) => TogglePopup();
        _icon.ForceCreate(enablesEfficiencyMode: false);

        _monitor.Changed += Render;
        _monitor.SetFavorite(_settings.Favorite);
        _monitor.Start();
        Render();

        SystemEvents.SessionSwitch            += OnSessionSwitch;
        SystemEvents.PowerModeChanged         += OnPowerModeChanged;
        SystemEvents.UserPreferenceChanged    += OnUserPreferenceChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

        // Installed: keep the Run entry pointing at the current exe. Development builds never register themselves.
        if (_isInstalled)
            Autostart.Apply(_settings.StartWithWindows);

        if (!_settings.WelcomeShown)
        {
            _icon.ShowNotification("glei läuft im Infobereich",
                "Klick aufs Symbol, um deine Haltestelle festzulegen. Tipp: Über Einstellungen → Taskleiste kannst du das Symbol dauerhaft anzeigen.",
                NotificationIcon.Info);
            _settings.WelcomeShown = true;
            _store.Save(_settings);
        }
    }

    public void ShowPopup()
    {
        if (_popup is null) return;
        _popup.ShowPopup();
    }

    private void TogglePopup()
    {
        if (_popup is null) return;

        // The click on the icon first deactivates (hides) the popup – don't reopen it right away
        if (_popup.IsVisible) _popup.HidePopup();
        else if (DateTime.UtcNow - _popup.HiddenAtUtc > ReopenGuard) _popup.ShowPopup();
    }

    private void SetFavorite(Favorite favorite)
    {
        Log.Information("Favorite: {Stop} line {Line} {Direction}", favorite.StopId, favorite.Line, favorite.Direction);
        _settings.Favorite = favorite;
        _store.Save(_settings);
        _monitor.SetFavorite(favorite);
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    private void Render()
    {
        if (_icon is null) return;

        var view = TrayPresenter.Present(_settings.Favorite, _monitor.Snapshot, DateTime.UtcNow);
        if (view != _shown)
        {
            var icon = view.Text is null
                ? IconRenderer.AppIcon()
                : IconRenderer.Render(view.Text, _lightTaskbar, view.Dimmed, view.RealtimeDot);
            _icon.Icon        = icon;
            _icon.ToolTipText = view.ToolTip;
            _currentIcon?.Dispose();
            _currentIcon = icon;
            _shown       = view;
        }

        if (_popup?.IsVisible == true)
        {
            _viewModel!.RenderFavorite();
            _viewModel.RenderStop();
        }
    }

    // ── Context menu ─────────────────────────────────────────────────────────

    private ContextMenu BuildMenu()
    {
        var refresh = new MenuItem { Header = "Aktualisieren" };
        refresh.Click += async (_, _) => await _monitor.RefreshAsync();

        var autostart = new MenuItem { Header = "Mit Windows starten", IsCheckable = true, IsChecked = _settings.StartWithWindows };
        autostart.Click += (_, _) =>
        {
            _settings.StartWithWindows = autostart.IsChecked;
            _store.Save(_settings);
            Autostart.Apply(autostart.IsChecked);
        };

        var info = new MenuItem { Header = "Info" };
        // After the menu has closed, so the window gets the focus
        info.Click += (_, _) => _dispatcher.BeginInvoke(InfoWindow.ShowSingle, DispatcherPriority.ApplicationIdle);

        var exit = new MenuItem { Header = "Beenden" };
        exit.Click += (_, _) => Application.Current.Shutdown();

        return new ContextMenu { Items = { refresh, autostart, new Separator(), info, exit } };
    }

    // ── System events (raised on other threads) ──────────────────────────────

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.ConsoleDisconnect or SessionSwitchReason.RemoteDisconnect)
            _monitor.Pause();
        else if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.ConsoleConnect or SessionSwitchReason.RemoteConnect)
            _monitor.Resume();
    });

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        if (e.Mode == PowerModes.Suspend) _monitor.Pause();
        else if (e.Mode == PowerModes.Resume) _monitor.Resume();
    });

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        if (e.IsAvailable)
            _dispatcher.BeginInvoke(() => _monitor.RefreshAsync());
    }

    /// <summary>Taskbar switched between light and dark: redraw the number in the matching color.</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => _dispatcher.BeginInvoke(() =>
    {
        var light = IconRenderer.IsTaskbarLight();
        if (light == _lightTaskbar) return;
        _lightTaskbar = light;
        _shown = null;
        Render();
    });

    public void Dispose()
    {
        SystemEvents.SessionSwitch            -= OnSessionSwitch;
        SystemEvents.PowerModeChanged         -= OnPowerModeChanged;
        SystemEvents.UserPreferenceChanged    -= OnUserPreferenceChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

        _monitor.Dispose();
        _popup?.Close();
        _icon?.Dispose();
        _currentIcon?.Dispose();
    }
}
