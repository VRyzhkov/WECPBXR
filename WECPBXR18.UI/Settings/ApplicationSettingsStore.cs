using System.IO;
using System.Text.Json;

namespace WECPBXR18.UI.Settings;

public sealed class ApplicationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ApplicationSettingsStore(string? settingsPath = null)
    {
        SettingsPath = settingsPath ?? GetDefaultSettingsPath();
    }

    public string SettingsPath { get; }

    public ApplicationSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            ApplicationSettings defaultSettings = new();
            Save(defaultSettings);
            return defaultSettings;
        }

        using FileStream stream = File.OpenRead(SettingsPath);
        return JsonSerializer.Deserialize<ApplicationSettings>(stream, JsonOptions) ?? new ApplicationSettings();
    }

    public void Save(ApplicationSettings settings)
    {
        string? directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using FileStream stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, JsonOptions);
    }

    private static string GetDefaultSettingsPath()
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        string sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.json"));
        return File.Exists(sourcePath) ? sourcePath : outputPath;
    }
}
