using System.IO;
using Whisper.net;

namespace LocalDictate.Services;

public sealed class TranscriptionService : IDisposable
{
    private readonly ModelManager _models;
    private readonly AppSettings _settings;
    private readonly AppLogger _logger;
    private WhisperFactory? _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TranscriptionService(ModelManager models, AppSettings settings, AppLogger logger)
    {
        _models = models;
        _settings = settings;
        _logger = logger;
    }

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_factory is not null) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_factory is not null) return;
            if (!_models.ModelExists)
            {
                throw new InvalidOperationException("Модель Whisper не найдена.");
            }

            _factory = WhisperFactory.FromPath(_models.ModelPath);
            _logger.Info("whisper factory loaded");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> TranscribeWavAsync(byte[] wavBytes, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct);
        if (_factory is null || wavBytes.Length == 0)
        {
            return string.Empty;
        }

        await _gate.WaitAsync(ct);
        try
        {
            await using var processor = _factory.CreateBuilder()
                .WithLanguage(ResolveLanguage())
                .Build();

            await using var stream = new MemoryStream(wavBytes);
            var parts = new List<string>();
            await foreach (var segment in processor.ProcessAsync(stream, ct))
            {
                var text = segment.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }

            var result = string.Join(' ', parts).Trim();
            _logger.Info($"transcribed {result.Length} chars");
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolveLanguage()
    {
        return _settings.AsrLanguage.ToLowerInvariant() switch
        {
            "ru" => "ru",
            "en" => "en",
            _ => "auto",
        };
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _gate.Dispose();
    }
}
