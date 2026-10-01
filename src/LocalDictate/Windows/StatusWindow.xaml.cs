using System.Windows;
using System.Windows.Media.Animation;
using LocalDictate.Services;

namespace LocalDictate.Windows;

public partial class StatusWindow : Window
{
    private readonly DictationController _controller;
    private readonly UpdateService _updates;
    private readonly AppSettings _settings;
    private readonly AppPaths _paths;
    private bool _loadingSettings = true;

    public StatusWindow(
        DictationController controller,
        AppSettings settings,
        ModelManager models,
        AppPaths paths,
        UpdateService updates)
    {
        InitializeComponent();
        _controller = controller;
        _updates = updates;
        _settings = settings;
        _paths = paths;
        _controller.AttachWindow(this);
        SoundCuesBox.IsChecked = settings.PlaySoundCues;
        _loadingSettings = false;

        LanguageValue.Text = settings.AsrLanguage.ToLowerInvariant() switch
        {
            "ru" => "Русский",
            "en" => "English",
            _ => "Авто",
        };
        OfflineValue.Text = settings.OfflineOnly ? "Да" : "Нет";
        PathsText.Text =
            $"Модель: {(models.ModelExists ? "есть" : "будет загружена один раз")}\n" +
            $"Папка: {paths.Root}";

        Opacity = 0;
        Loaded += (_, _) =>
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        };
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    public void ShowUpdateState()
    {
        UpdateStatusText.Text = _updates.Status;
        UpdateButton.IsEnabled = !_updates.IsBusy;
        if (_updates.Available is null)
        {
            UpdateButton.Content = "Проверить";
            UpdateButton.Style = (Style)FindResource("QuietButton");
        }
        else
        {
            UpdateButton.Content = "Обновить";
            UpdateButton.Style = (Style)FindResource("AccentButton");
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        string? notice = null;
        try
        {
            UpdateButton.IsEnabled = false;
            if (_updates.Available is null)
            {
                await _updates.CheckAsync();
                return;
            }

            if (_controller.IsDictating)
            {
                notice = "Сначала остановите запись.";
                return;
            }

            var offer = _updates.Available;
            var answer = MessageBox.Show(
                $"Скачать {offer.Tag} целиком (программа, Whisper и CUDA), сверить sha256 и заменить папку установки?\n\nНастройки и модель не удаляются. Приложение закроется и откроется снова.",
                "LocalDictate",
                MessageBoxButton.YesNo,
                MessageBoxImage.None);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var restart = await _updates.DownloadAndApplyAsync();
            if (restart)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            notice = ex.Message;
        }
        finally
        {
            if (System.Windows.Application.Current is not null)
            {
                ShowUpdateState();
                if (notice is not null)
                {
                    UpdateStatusText.Text = notice;
                    UpdateButton.IsEnabled = true;
                }
            }
        }
    }

    public void ApplyVisual(RecordingVisual visual)
    {
        LiveWave.IsLive = visual.Active;
        LiveWave.Level = visual.Level;
    }

    private void SoundCues_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings)
        {
            return;
        }

        _settings.PlaySoundCues = SoundCuesBox.IsChecked == true;
        SettingsStore.Save(_paths, _settings);
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
