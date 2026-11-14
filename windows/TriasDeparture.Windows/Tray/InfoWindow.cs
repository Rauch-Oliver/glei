using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace TriasDeparture.Windows.Tray;

/// <summary>
/// "Info" from the tray menu. A real window instead of MessageBox: a MessageBox opened from the
/// tray menu takes the menu's hidden host window as owner and closes together with the menu.
/// </summary>
public sealed class InfoWindow : Window
{
    private static InfoWindow? _open;

    public static void ShowSingle()
    {
        if (_open is not null)
        {
            _open.Activate();
            return;
        }

        _open = new InfoWindow();
        _open.Closed += (_, _) => _open = null;
        _open.Show();
        _open.Activate();
    }

    private InfoWindow()
    {
        Title                 = "Info – glei";
        SizeToContent         = SizeToContent.WidthAndHeight;
        ResizeMode            = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost               = true;

        var link = new Hyperlink(new Run(AppInfo.RepositoryUrl)) { NavigateUri = new Uri(AppInfo.RepositoryUrl) };
        link.RequestNavigate += (_, e) =>
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        };

        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true, IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            MinWidth = 320,
            Children =
            {
                new TextBlock { Text = "glei", FontSize = 20, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $"Version {AppInfo.Version}", Opacity = 0.65, Margin = new Thickness(0, 2, 0, 12) },
                new TextBlock { Text = "Live-Abfahrten für Bim und Bus in Graz." },
                new TextBlock { Text = "Daten: Verbund Linie (TRIAS)", Margin = new Thickness(0, 4, 0, 0) },
                new TextBlock(link) { Margin = new Thickness(0, 4, 0, 20) },
                ok
            }
        };
    }
}
