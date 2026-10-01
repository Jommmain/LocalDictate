using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace LocalDictate.Services;

/// <summary>
/// Checks the stable GitHub Releases channel and replaces the whole install folder
/// (exe plus Whisper/CUDA natives). User data under LocalAppData\LocalDictate is not mirrored.
/// </summary>
public sealed class UpdateService
{
    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly HttpClient _http;
    private readonly string _installDir;

    public UpdateService(AppPaths paths, AppLogger logger, string? installDir = null)
    {
        _paths = paths;
        _logger = logger;
        _installDir = Path.GetFullPath(installDir ?? AppContext.BaseDirectory);
        _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"LocalDictate/{ReleaseChannel.CurrentVersion()}");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public ReleaseOffer? Available { get; private set; }

    public string Status { get; private set; } = "Канал: стабильные теги vX.Y.Z.";

    public bool IsBusy { get; private set; }

    public event EventHandler? StateChanged;

    public async Task CheckAsync(CancellationToken ct = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        Report("Проверяю стабильные релизы GitHub…");
        try
        {
            var json = await _http.GetStringAsync(ReleaseChannel.ReleasesUrl, ct);
            var current = ReleaseChannel.CurrentVersion();
            Available = ReleaseChannel.ChooseNewer(ReleaseChannel.ParseReleases(json), current);
            if (Available is null)
            {
                Report($"Установлена последняя стабильная версия {current}.");
                _logger.Info($"update check: current {current} is latest stable");
            }
            else
            {
                Report($"Доступно {Available.Tag}. «Обновить» скачает весь пакет, проверит sha256 и перезапустит приложение.");
                _logger.Info($"update available {Available.Tag} asset={Available.Asset.Name}");
            }
        }
        catch (Exception ex)
        {
            _logger.Info("update check skipped: " + ex.Message);
            Report("Не удалось проверить обновления. Диктовка работает и без сети.");
        }
        finally
        {
            IsBusy = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<bool> DownloadAndApplyAsync(CancellationToken ct = default)
    {
        var offer = Available ?? throw new InvalidOperationException("Нет доступного обновления.");
        if (string.IsNullOrWhiteSpace(offer.Asset.Digest))
        {
            throw new InvalidOperationException("У релиза нет sha256. Автообновление остановлено.");
        }

        EnsureInstallCanBeReplaced();
        IsBusy = true;
        Report($"Скачиваю {offer.Asset.Name}…");
        try
        {
            var updatesDir = Path.Combine(_paths.Root, "updates");
            Directory.CreateDirectory(updatesDir);
            var zipPath = Path.Combine(updatesDir, offer.Asset.Name);
            var hex = await DownloadAsync(offer.Asset.DownloadUrl, zipPath, ct);
            if (!ReleaseChannel.DigestMatches(offer.Asset.Digest, hex))
            {
                File.Delete(zipPath);
                throw new InvalidOperationException("Контрольная сумма не совпала. Файл удалён, установка не менялась.");
            }

            Report("Распаковываю пакет…");
            var extracted = Path.Combine(updatesDir, "staged", offer.Version.ToString());
            if (Directory.Exists(extracted))
            {
                Directory.Delete(extracted, recursive: true);
            }

            Directory.CreateDirectory(extracted);
            ZipFile.ExtractToDirectory(zipPath, extracted);
            var payload = ReleaseChannel.ResolvePayloadRoot(extracted);
            if (!ReleaseChannel.PayloadHasRuntime(payload))
            {
                throw new InvalidOperationException("В пакете нет LocalDictate.exe и библиотек Whisper. Папка установки не менялась.");
            }

            var exe = Path.Combine(_installDir, "LocalDictate.exe");
            var scriptPath = Path.Combine(updatesDir, "apply-update.cmd");
            var script = ReleaseChannel.BuildApplyScript(Environment.ProcessId, payload, _installDir, exe);
            await File.WriteAllTextAsync(scriptPath, script, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true), ct);

            Report("Перезапуск и замена папки установки…");
            _logger.Info($"update apply {offer.Tag} -> {_installDir}");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            });
            return true;
        }
        finally
        {
            IsBusy = false;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void EnsureInstallCanBeReplaced()
    {
        if (!File.Exists(Path.Combine(_installDir, "LocalDictate.exe")))
        {
            throw new InvalidOperationException("Эта копия запущена не из установленной папки. Обновление заменит только каталог с LocalDictate.exe.");
        }

        if (ReleaseChannel.IsDevOutput(_installDir))
        {
            throw new InvalidOperationException("Это папка сборки (bin/obj). Установите приложение и обновляйте его оттуда.");
        }

        if (!ReleaseChannel.IsSafeInstallDirectory(_installDir, _paths.Root))
        {
            throw new InvalidOperationException("Папка установки совпадает с данными приложения. Обновление отменено, чтобы не стереть модель и настройки.");
        }
    }

    private async Task<string> DownloadAsync(string url, string destination, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 128];
        long total = 0;
        long nextReport = 8L * 1024 * 1024;
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            sha.AppendData(buffer, 0, read);
            total += read;
            if (total >= nextReport)
            {
                Report($"Скачано {total / (1024 * 1024)} МБ…");
                nextReport += 8L * 1024 * 1024;
            }
        }

        return Convert.ToHexString(sha.GetHashAndReset());
    }

    private void Report(string status)
    {
        Status = status;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
