using Avalonia.Platform;
using Serilog;

namespace TriasDeparture.Linux.Data;

/// <summary>
/// Freedesktop integration of an installed (AppImage) glei: autostart entry ("Beim Anmelden starten"),
/// app-menu entry and icon. Development builds never touch these files.
/// </summary>
public sealed class DesktopIntegration(string configHome, string dataHome, string executable)
{
    public const string FileName = "glei.desktop";

    public string AutostartFile => Path.Combine(configHome, "autostart", FileName);
    public string MenuFile => Path.Combine(dataHome, "applications", FileName);
    public string IconFile => Path.Combine(dataHome, "icons", "hicolor", "256x256", "apps", "glei.png");

    /// <summary>
    /// The path to put into Exec=: inside an AppImage the process runs from a temporary mount,
    /// so the AppImage file itself ($APPIMAGE) must be started.
    /// </summary>
    public static string? CurrentExecutable() =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage ? appImage : Environment.ProcessPath;

    public static DesktopIntegration? ForCurrentUser() =>
        CurrentExecutable() is { } exe ? new DesktopIntegration(LinuxPaths.ConfigHome, LinuxPaths.DataHome, exe) : null;

    /// <summary>Contents of glei.desktop (Desktop Entry Specification 1.5).</summary>
    public string Entry(bool autostart)
    {
        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            "Version=1.5",
            "Name=glei",
            "GenericName=Abfahrten",
            "Comment=Live-Abfahrten für Bim und Bus in Graz",
            $"Exec={QuoteExec(executable)}",
            "Icon=glei",
            "Terminal=false",
            "Categories=Utility;",
            "Keywords=Bus;Bim;Straßenbahn;Abfahrt;Graz;"
        };
        if (autostart)
        {
            lines.Add("X-GNOME-Autostart-enabled=true");
            lines.Add("NoDisplay=true");
        }
        return string.Join('\n', lines) + "\n";
    }

    public bool IsAutostartEnabled => File.Exists(AutostartFile);

    public void ApplyAutostart(bool enabled)
    {
        try
        {
            if (enabled) Write(AutostartFile, Entry(autostart: true));
            else File.Delete(AutostartFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Updating autostart failed");
        }
    }

    /// <summary>App-menu entry + icon, so glei can be found and started like any other app.</summary>
    public void InstallMenuEntry()
    {
        try
        {
            Write(MenuFile, Entry(autostart: false));
            if (!File.Exists(IconFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(IconFile)!);
                using var png = AssetLoader.Open(new Uri("avares://glei/Assets/glei.png"));
                using var file = File.Create(IconFile);
                png.CopyTo(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Installing the app-menu entry failed");
        }
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Only rewrite when changed (e.g. the AppImage moved), so file watchers of the desktop stay quiet
        if (File.Exists(path) && File.ReadAllText(path) == content) return;
        File.WriteAllText(path, content);
    }

    /// <summary>Exec= quoting: arguments with reserved characters in double quotes, with \ " ` $ escaped.</summary>
    internal static string QuoteExec(string path)
    {
        if (path.All(c => char.IsAsciiLetterOrDigit(c) || "/._-+".Contains(c)))
            return path;
        var escaped = path.Replace(@"\", @"\\").Replace("\"", "\\\"").Replace("`", "\\`").Replace("$", "\\$");
        // In a desktop file, a literal backslash has to be escaped once more (string value level)
        return "\"" + escaped.Replace(@"\", @"\\") + "\"";
    }
}
