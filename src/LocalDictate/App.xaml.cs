using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using LocalDictate.Services;
using LocalDictate.Windows;
using Forms = System.Windows.Forms;

namespace LocalDictate;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _tray;
    private System.Drawing.Icon? _trayIcon;
    private DictationController? _controller;
    private StatusWindow? _statusWindow;
    private RecordingOverlay? _overlay;
    private UpdateService? _updates;
    private bool _updateAnnounced;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ThemeService.ApplySystemTheme();

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
        _updates = new UpdateService(paths, logger);

        _controller = new DictationController(
            settings, recorder, asr, inserter, history, hotkeys, logger);

        _overlay = new RecordingOverlay();
        _statusWindow = new StatusWindow(_controller, settings, models, paths, _updates);
        _updates.StateChanged += (_, _) =>
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    _statusWindow?.ShowUpdateState();
                    if (_updates.Available is not null && !_updateAnnounced)
                    {
                        _updateAnnounced = true;
                        _tray?.ShowBalloonTip(
                            5000,
                            "LocalDictate",
                            $"{_updates.Available.Tag} — откройте окно и нажмите «Обновить».",
                            Forms.ToolTipIcon.Info);
                    }
                });
            }
            catch (InvalidOperationException)
            {
                // The dispatcher is shutting down.
            }
        };
        _controller.StatusChanged += (_, status) =>
            Dispatcher.Invoke(() => _statusWindow?.SetStatus(status));
        _controller.VisualChanged += (_, visual) =>
        {
            _overlay?.ShowForState(visual);
            _statusWindow?.ApplyVisual(visual);
        };

        ApplyWindowIcon(_statusWindow);
        SetupTray();
        _statusWindow.Show();
        // Ensure HWND exists before registering the global hotkey.
        _ = new System.Windows.Interop.WindowInteropHelper(_statusWindow).EnsureHandle();
        _ = CheckForUpdatesAsync();

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
        _trayIcon = new System.Drawing.Icon(IconFilePath);
        _tray = new Forms.NotifyIcon
        {
            Visible = true,
            Text = "LocalDictate",
            Icon = _trayIcon,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };

        _tray.ContextMenuStrip.Items.Add("Открыть", null, (_, _) =>
        {
            _statusWindow?.Show();
            _statusWindow?.Activate();
        });
        _tray.ContextMenuStrip.Items.Add("Проверить обновления", null, (_, _) =>
        {
            _statusWindow?.Show();
            _statusWindow?.Activate();
            _ = CheckForUpdatesAsync();
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
        CloseOverlay();
        DisposeTray();
        Shutdown();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_updates is null)
        {
            return;
        }

        await _updates.CheckAsync();
    }

    private static string IconFilePath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "LocalDictate.ico");

    private static void ApplyWindowIcon(Window window)
    {
        if (!File.Exists(IconFilePath))
        {
            return;
        }

        window.Icon = BitmapFrame.Create(new Uri(IconFilePath, UriKind.Absolute));
    }

    private void CloseOverlay()
    {
        if (_overlay is null)
        {
            return;
        }

        try
        {
            _overlay.Close();
        }
        catch
        {
            // The overlay is a tool window; shutdown should not depend on it.
        }

        _overlay = null;
    }

    private void DisposeTray()
    {
        if (_tray is not null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }

        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _controller?.Dispose();
        CloseOverlay();
        DisposeTray();
        base.OnExit(e);
    }
}
