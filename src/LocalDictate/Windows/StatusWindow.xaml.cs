using System.Windows;
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

        PathsText.Text =
            $"Модель: {(models.ModelExists ? "есть" : "будет загружена один раз")}\n" +
            $"Папка: {paths.Root}\n" +
            $"Офлайн: {(settings.OfflineOnly ? "да" : "нет")} · язык ASR: {settings.AsrLanguage}";
    }

    public void SetStatus(string status)
    {
        StatusText.Text = status;
    }

    private void Hide_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Close to tray instead of exiting.
        e.Cancel = true;
        Hide();
    }
}
