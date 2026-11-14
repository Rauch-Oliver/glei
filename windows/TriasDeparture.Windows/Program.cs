using Velopack;

namespace TriasDeparture.Windows;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must run first: handles install/update/uninstall hooks and exits in those cases
        VelopackApp.Build().Run();

        using var instance = new SingleInstance("glei.tray");
        if (!instance.IsFirst)
        {
            // Second start (e.g. Start menu while running): just open the popup of the running instance
            instance.SignalFirst();
            return;
        }

        var app = new App(instance);
        app.InitializeComponent();
        app.Run();
    }
}
