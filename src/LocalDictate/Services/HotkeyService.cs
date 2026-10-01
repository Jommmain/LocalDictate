using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;

namespace LocalDictate.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4C44; // 'LD'
    private readonly AppLogger _logger;
    private HwndSource? _source;
    private Action? _callback;
    private bool _registered;

    public HotkeyService(AppLogger logger)
    {
        _logger = logger;
    }

    public void Register(WindowInteropHelper helper, AppSettings settings, Action callback)
    {
        _callback = callback;
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);

        var modifiers = ParseModifiers(settings.HotkeyModifiers);
        var key = ParseKey(settings.HotkeyKey);

        if (!RegisterHotKey(helper.Handle, HotkeyId, modifiers, (uint)key))
        {
            throw new InvalidOperationException(
                "Не удалось зарегистрировать глобальный хоткей. Возможно, он уже занят.");
        }

        _registered = true;
        _logger.Info($"hotkey registered: {settings.HotkeyModifiers}+{settings.HotkeyKey}");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _callback?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static uint ParseModifiers(string value) => value.ToLowerInvariant() switch
    {
        "alt" => MOD_ALT,
        "control" or "ctrl" => MOD_CONTROL,
        "shift" => MOD_SHIFT,
        "win" => MOD_WIN,
        _ => 0,
    };

    private static Keys ParseKey(string value)
    {
        if (Enum.TryParse<Keys>(value, ignoreCase: true, out var key))
        {
            return key;
        }

        return Keys.RControlKey;
    }

    public void Dispose()
    {
        if (_registered && _source is not null)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
            _registered = false;
        }
    }

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
