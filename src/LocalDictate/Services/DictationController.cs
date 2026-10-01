using System.Windows;
using System.Windows.Interop;
using LocalDictate.Services;

namespace LocalDictate.Services;

public sealed class DictationController : IDisposable
{
    private readonly AppSettings _settings;
    private readonly AudioRecorder _recorder;
    private readonly TranscriptionService _asr;
    private readonly TextInserter _inserter;
    private readonly HistoryStore _history;
    private readonly HotkeyService _hotkeys;
    private readonly AppLogger _logger;
    private readonly object _gate = new();
    private bool _busy;
    private bool _started;
    private DateTime _startedAt;
    private Window? _hostWindow;
    private int _visualQueued;
    private RecordingVisual _latestVisual;
    private volatile bool _captureLevels;

    public event EventHandler<string>? StatusChanged;

    public event EventHandler<RecordingVisual>? VisualChanged;

    public bool IsDictating
    {
        get
        {
            lock (_gate)
            {
                return _busy || _recorder.IsRecording;
            }
        }
    }

    public DictationController(
        AppSettings settings,
        AudioRecorder recorder,
        TranscriptionService asr,
        TextInserter inserter,
        HistoryStore history,
        HotkeyService hotkeys,
        AppLogger logger)
    {
        _settings = settings;
        _recorder = recorder;
        _asr = asr;
        _inserter = inserter;
        _history = history;
        _hotkeys = hotkeys;
        _logger = logger;
        _recorder.LevelAvailable += (_, level) =>
        {
            if (_captureLevels && _recorder.IsRecording)
            {
                PublishVisual(true, "Запись", level);
            }
        };
    }

    public void AttachWindow(Window window) => _hostWindow = window;

    public void Start()
    {
        if (_started) return;
        if (_hostWindow is null)
        {
            throw new InvalidOperationException("Host window is required for global hotkeys.");
        }

        var helper = new WindowInteropHelper(_hostWindow);
        helper.EnsureHandle();
        _hotkeys.Register(helper, _settings, OnHotkey);
        _started = true;
        RaiseStatus("Служба запущена");
    }

    private void OnHotkey() => _ = Task.Run(HandleHotkeyAsync);

    private async Task HandleHotkeyAsync()
    {
        lock (_gate)
        {
            if (_busy) return;
        }

        if (!_recorder.IsRecording)
        {
            _startedAt = DateTime.UtcNow;
            _captureLevels = true;
            _recorder.Start();
            SoundCues.Play(_settings.PlaySoundCues, starting: true);
            RaiseStatus("Идёт запись…");
            PublishVisual(true, "Запись", 0.05f);
            return;
        }

        lock (_gate) _busy = true;
        try
        {
            _captureLevels = false;
            SoundCues.Play(_settings.PlaySoundCues, starting: false);
            RaiseStatus("Распознавание…");
            PublishVisual(true, "Распознавание", 0f);
            var wav = _recorder.Stop();
            var audioSeconds = (DateTime.UtcNow - _startedAt).TotalSeconds;
            var text = await _asr.TranscribeWavAsync(wav);
            if (string.IsNullOrWhiteSpace(text))
            {
                RaiseStatus("Пусто — попробуйте ещё раз");
                PublishVisual(false, "", 0f);
                return;
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
                _inserter.Paste(text, _settings.PressEnterAfterPaste));

            _history.Add(text, audioSeconds);
            RaiseStatus($"Вставлено: {Truncate(text, 60)}");
            PublishVisual(false, "", 0f);
        }
        catch (Exception ex)
        {
            _logger.Error("dictation failed", ex);
            RaiseStatus($"Ошибка: {ex.Message}");
            PublishVisual(false, "", 0f);
        }
        finally
        {
            lock (_gate) _busy = false;
        }
    }

    private void RaiseStatus(string status) => StatusChanged?.Invoke(this, status);

    private void PublishVisual(bool active, string phase, float level)
    {
        _latestVisual = new RecordingVisual(active, phase, level);
        if (Interlocked.Exchange(ref _visualQueued, 1) == 1)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            Interlocked.Exchange(ref _visualQueued, 0);
            VisualChanged?.Invoke(this, _latestVisual);
            return;
        }

        try
        {
            dispatcher.BeginInvoke(new Action(() =>
            {
                Interlocked.Exchange(ref _visualQueued, 0);
                VisualChanged?.Invoke(this, _latestVisual);
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _visualQueued, 0);
        }
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "…";

    public void Dispose()
    {
        _hotkeys.Dispose();
        _recorder.Dispose();
        _asr.Dispose();
    }
}
