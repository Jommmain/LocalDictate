using System.Runtime.InteropServices;
using System.Windows;

namespace LocalDictate.Services;

public sealed class TextInserter
{
    private readonly AppLogger _logger;

    public TextInserter(AppLogger logger)
    {
        _logger = logger;
    }

    public void Paste(string text, bool pressEnter)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string? previous = null;
        try
        {
            if (Clipboard.ContainsText())
            {
                previous = Clipboard.GetText();
            }
        }
        catch
        {
            // clipboard busy
        }

        Clipboard.SetText(text);

        // Ctrl+V
        keybd_event(0x11, 0, 0, UIntPtr.Zero);
        keybd_event(0x56, 0, 0, UIntPtr.Zero);
        keybd_event(0x56, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

        if (pressEnter)
        {
            Thread.Sleep(40);
            keybd_event(0x0D, 0, 0, UIntPtr.Zero);
            keybd_event(0x0D, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        if (previous is not null)
        {
            var restore = previous;
            var pasted = text;
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                try
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (Clipboard.ContainsText() && Clipboard.GetText() == pasted)
                        {
                            Clipboard.SetText(restore);
                        }
                    });
                }
                catch
                {
                    // ignore
                }
            });
        }

        _logger.Info($"pasted {text.Length} chars");
    }

    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
