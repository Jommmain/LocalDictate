using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalDictate;

public sealed class AppSettings
{
    [JsonPropertyName("offlineOnly")]
    public bool OfflineOnly { get; set; } = true;

    [JsonPropertyName("allowModelDownloadOnce")]
    public bool AllowModelDownloadOnce { get; set; } = true;

    [JsonPropertyName("uiLanguage")]
    public string UiLanguage { get; set; } = "ru";

    /// <summary>
    /// Whisper language code: "ru", "en", or "auto".
    /// </summary>
    [JsonPropertyName("asrLanguage")]
    public string AsrLanguage { get; set; } = "auto";

    [JsonPropertyName("modelFileName")]
    public string ModelFileName { get; set; } = "ggml-small.bin";

    [JsonPropertyName("hotkeyModifiers")]
    public string HotkeyModifiers { get; set; } = "None";

    [JsonPropertyName("hotkeyKey")]
    public string HotkeyKey { get; set; } = "RControlKey";

    [JsonPropertyName("pressEnterAfterPaste")]
    public bool PressEnterAfterPaste { get; set; }

    [JsonPropertyName("historyLimit")]
    public int HistoryLimit { get; set; } = 50;

    [JsonPropertyName("modelDownloaded")]
    public bool ModelDownloaded { get; set; }
}

public sealed class AppPaths
{
    public required string Root { get; init; }
    public required string ModelsDir { get; init; }
    public required string SettingsFile { get; init; }
    public required string HistoryFile { get; init; }
    public required string LogFile { get; init; }

    public static AppPaths Create()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalDictate");
        Directory.CreateDirectory(root);
        var models = Path.Combine(root, "models");
        Directory.CreateDirectory(models);
        var logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(logs);

        return new AppPaths
        {
            Root = root,
            ModelsDir = models,
            SettingsFile = Path.Combine(root, "settings.json"),
            HistoryFile = Path.Combine(root, "history.json"),
            LogFile = Path.Combine(logs, "localdictate.log"),
        };
    }
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static AppSettings LoadOrCreate(AppPaths paths)
    {
        if (File.Exists(paths.SettingsFile))
        {
            var json = File.ReadAllText(paths.SettingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }

        var defaultsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.default.json");
        AppSettings settings;
        if (File.Exists(defaultsPath))
        {
            var json = File.ReadAllText(defaultsPath);
            settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        else
        {
            settings = new AppSettings();
        }

        Save(paths, settings);
        return settings;
    }

    public static void Save(AppPaths paths, AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(paths.SettingsFile, json);
    }
}
