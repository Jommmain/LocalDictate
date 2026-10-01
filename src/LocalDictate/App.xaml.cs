using System.Windows;
using LocalDictate.Services;
using LocalDictate.Windows;
using Forms = System.Windows.Forms;

namespace LocalDictate;

public partial class App : Application
{
    private Forms.NotifyIcon? _tray;
    private DictationController? _controller;
    private StatusWindow? _statusWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var paths = AppPaths.Create();
        var settings = SettingsStore.LoadOrCreate(paths);
        var logger = new AppLogger(paths.LogFile);
        var offline = new OfflinePolicy(settings, logger);
        var history = new HistoryStore(paths.HistoryFile, settings.HistoryLimit);
        var models = new ModelManager(paths, settings, offline, logger);
        var recorder = new AudioRecorder(logger);
        var asr = new TranscriptionService(models, settings, logger);
        var inserter = new TextInserter(logger);
        var hotkeys = new HotkeyService(logger);

        _controller = new DictationController(
            settings, recorder, asr, inserter, history, hotkeys, logger);

        _statusWindow = new StatusWindow(_controller, settings, models, paths);
        _controller.StatusChanged += (_, status) =>
            Dispatcher.Invoke(() => _statusWindow?.SetStatus(status));

        SetupTray();
        _statusWindow.Show();
        // Ensure HWND exists before registering the global hotkey.
        _ = new System.Windows.Interop.WindowInteropHelper(_statusWindow).EnsureHandle();

        try
        {
            await models.EnsureModelReadyAsync();
            _controller.Start();
            _statusWindow.SetStatus("Готово. Правый Ctrl — диктовка.");
        }
        catch (Exception ex)
        {
            logger.Error("startup failed", ex);
            _statusWindow.SetStatus($"Ошибка: {ex.Message}");
            MessageBox.Show(
                $"Не удалось запустить LocalDictate.\n\n{ex.Message}",
                "LocalDictate",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetupTray()
    {
        _tray = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "LocalDictate",
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _tray.ContextMenuStrip.Items.Add("Открыть", null, (_, _) =>
        {
            _statusWindow?.Show();
            _statusWindow?.Activate();
        });
        _tray.ContextMenuStrip.Items.Add("Выход", null, (_, _) => ShutdownApp());
        _tray.DoubleClick += (_, _) =>
        {
            _statusWindow?.Show();
            _statusWindow?.Activate();
        };
    }

    private void ShutdownApp()
    {
        _controller?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}
