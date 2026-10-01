using System.IO;
using System.Net.Http;
using LocalDictate.Services;

namespace LocalDictate.Services;

/// <summary>
/// Blocks network use after the speech model is present.
/// One-time model download is allowed only when explicitly enabled in settings.
/// </summary>
public sealed class OfflinePolicy
{
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;

    public OfflinePolicy(AppSettings settings, AppLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public bool CanUseNetworkForModelDownload(bool modelExists)
    {
        if (!_settings.OfflineOnly)
        {
            return true;
        }

        if (modelExists)
        {
            _logger.Info("offline policy: model present, network blocked");
            return false;
        }

        if (!_settings.AllowModelDownloadOnce || _settings.ModelDownloaded)
        {
            _logger.Info("offline policy: one-time download already used or disabled");
            return false;
        }

        return true;
    }

    public void MarkModelDownloaded()
    {
        _settings.ModelDownloaded = true;
        _settings.AllowModelDownloadOnce = false;
    }
}

public sealed class ModelManager
{
    // Official ggml model host used by whisper.cpp / Whisper.net docs.
    private const string ModelUrl =
        "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";

    private readonly AppPaths _paths;
    private readonly AppSettings _settings;
    private readonly OfflinePolicy _offline;
    private readonly AppLogger _logger;

    public ModelManager(AppPaths paths, AppSettings settings, OfflinePolicy offline, AppLogger logger)
    {
        _paths = paths;
        _settings = settings;
        _offline = offline;
        _logger = logger;
    }

    public string ModelPath => Path.Combine(_paths.ModelsDir, _settings.ModelFileName);

    public bool ModelExists => File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 1_000_000;

    public async Task EnsureModelReadyAsync(CancellationToken ct = default)
    {
        if (ModelExists)
        {
            _logger.Info($"model ready: {ModelPath}");
            return;
        }

        if (!_offline.CanUseNetworkForModelDownload(modelExists: false))
        {
            throw new InvalidOperationException(
                "Модель не найдена, а офлайн-режим запрещает загрузку. " +
                $"Положите файл {_settings.ModelFileName} в {_paths.ModelsDir}");
        }

        _logger.Info($"downloading model once from Hugging Face → {ModelPath}");
        Directory.CreateDirectory(_paths.ModelsDir);
        var temp = ModelPath + ".partial";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await http.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = File.Create(temp))
        {
            await input.CopyToAsync(output, ct);
        }

        if (File.Exists(ModelPath))
        {
            File.Delete(ModelPath);
        }

        File.Move(temp, ModelPath);
        _offline.MarkModelDownloaded();
        SettingsStore.Save(_paths, _settings);
        _logger.Info("model download complete; offline mode locked");
    }
}
