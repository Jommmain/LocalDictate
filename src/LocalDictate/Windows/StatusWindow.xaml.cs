using System.Windows;
using System.Windows.Media.Animation;
using LocalDictate.Services;

namespace LocalDictate.Windows;

public partial class StatusWindow : Window
{
    private readonly DictationController _controller;

    public StatusWindow(
        DictationController controller,
        AppSettings settings,
        ModelManager models,
        AppPaths paths)
    {
        InitializeComponent();
        _controller = controller;
        _controller.AttachWindow(this);

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

    public void ApplyVisual(RecordingVisual visual)
    {
        LiveWave.IsLive = visual.Active;
        LiveWave.Level = visual.Level;
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
