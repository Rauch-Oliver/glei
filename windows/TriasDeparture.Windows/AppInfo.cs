using System.Reflection;
using System.Text.Json;

namespace TriasDeparture.Windows;

public static class AppInfo
{
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";

    public const string RepositoryUrl = "https://github.com/geisi93/glei";
}

public static class AppPaths
{
    /// <summary>%APPDATA%\glei – separate from the Velopack install folder, so updates never touch it.</summary>
    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "glei");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");
}

public static class AppConfig
{
    private const string DefaultApiBaseUrl = "https://glei-739185299212.europe-west1.run.app/";

    /// <summary>Env var GLEI_API_URL, otherwise appsettings.json next to the exe, otherwise the public glei API.</summary>
    public static Uri ApiBaseUrl { get; } = new(EnsureSlash(
        Environment.GetEnvironmentVariable("GLEI_API_URL") ?? ReadFromFile() ?? DefaultApiBaseUrl));

    private static string? ReadFromFile()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty("ApiBaseUrl", out var url) ? url.GetString() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string EnsureSlash(string url) => url.EndsWith('/') ? url : url + "/";
}
