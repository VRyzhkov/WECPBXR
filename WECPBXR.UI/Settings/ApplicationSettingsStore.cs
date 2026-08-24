using System.IO;
using System.Text.Json;

namespace WECPBXR.UI.Settings;

public sealed class ApplicationSettingsStore(string? settingsPath = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string SettingsPath { get; } = settingsPath ?? GetDefaultSettingsPath();

    public ApplicationSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            ApplicationSettings defaultSettings = new();
            Save(defaultSettings);
            return defaultSettings;
        }

        string json = File.ReadAllText(SettingsPath);
        ApplicationSettings settings = JsonSerializer.Deserialize<ApplicationSettings>(json, JsonOptions) ?? new ApplicationSettings();
        ApplyLegacyXrSettings(json, settings);
        return settings;
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

    private static void ApplyLegacyXrSettings(string json, ApplicationSettings settings)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("XR18", out JsonElement legacyXr))
        {
            return;
        }

        if (legacyXr.TryGetProperty("Address", out JsonElement address) &&
            address.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(address.GetString()))
        {
            settings.XR.Address = address.GetString()!;
        }

        if (legacyXr.TryGetProperty("AutoConnect", out JsonElement autoConnect) &&
            autoConnect.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            settings.XR.AutoConnect = autoConnect.GetBoolean();
        }

        if (legacyXr.TryGetProperty("PullOnConnect", out JsonElement pullOnConnect) &&
            pullOnConnect.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            settings.XR.PullOnConnect = pullOnConnect.GetBoolean();
        }
    }
}
