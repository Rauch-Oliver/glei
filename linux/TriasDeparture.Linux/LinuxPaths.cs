namespace TriasDeparture.Linux;

/// <summary>XDG base directories (https://specifications.freedesktop.org/basedir-spec/).</summary>
public static class LinuxPaths
{
    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Xdg(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } dir && Path.IsPathRooted(dir) ? dir : Path.Combine(Home, fallback);

    /// <summary>~/.config – settings and autostart entries.</summary>
    public static string ConfigHome => Xdg("XDG_CONFIG_HOME", ".config");

    /// <summary>~/.local/share – app menu entries and icons.</summary>
    public static string DataHome => Xdg("XDG_DATA_HOME", Path.Combine(".local", "share"));

    /// <summary>~/.local/state – logs.</summary>
    public static string StateHome => Xdg("XDG_STATE_HOME", Path.Combine(".local", "state"));

    /// <summary>~/.config/glei/settings.json – same format as on Windows.</summary>
    public static string SettingsFile => Path.Combine(ConfigHome, "glei", "settings.json");

    public static string LogDirectory => Path.Combine(StateHome, "glei", "logs");

    /// <summary>
    /// Per-user runtime dir for the single-instance socket and lock: $XDG_RUNTIME_DIR, or else a private
    /// folder in /tmp (never /tmp itself, which all users share).
    /// </summary>
    public static string RuntimeDirectory =>
        Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") is { Length: > 0 } dir && Path.IsPathRooted(dir)
            ? dir
            : PrivateTempDirectory();

    private static string PrivateTempDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"glei-{Environment.UserName}");
        try
        {
            // Only the owner may enter; if another user already took the name, opening the lock in it fails
            // and the app runs without single-instance (see SingleInstance.Acquire)
            Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        return dir;
    }
}
