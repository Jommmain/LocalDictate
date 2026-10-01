using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalDictate.Services;

namespace LocalDictate.Windows;

public partial class RecordingOverlay : Window
{
    private static readonly SolidColorBrush RecBrush = Frozen(0xFF, 0x3B, 0x30);
    private static readonly SolidColorBrush TranscribeBrush = Frozen(0x1F, 0x74, 0xE0);
    private bool _pinned;
    private int _pinGeneration;

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static string FormatElapsed(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0)
        {
            seconds = 0;
        }

        var whole = (int)seconds;
        return $"{whole / 60}:{whole % 60:00}";
    }

    public RecordingOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) => ClipToPill();
    }

    public void ShowForState(RecordingVisual visual)
    {
        if (!visual.Active)
        {
            Wave.IsLive = false;
            Wave.Level = 0;
            _pinned = false;
            _pinGeneration++;
            if (IsVisible)
            {
                Hide();
            }

            return;
        }

        var transcribing = visual.PhaseLabel.Contains("Распозн", StringComparison.Ordinal);
        PhaseText.Text = transcribing ? "···" : "Rec";
        TimerText.Text = FormatElapsed(visual.ElapsedSeconds);
        Wave.Level = visual.Level;
        Wave.IsLive = true;
        PulseDot.Fill = transcribing ? TranscribeBrush : RecBrush;

        if (_pinned)
        {
            return;
        }

        _pinned = true;
        var generation = ++_pinGeneration;
        ConfigureNonActivating();
        Opacity = 0;
        Show();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (generation != _pinGeneration || !IsVisible)
            {
                return;
            }

            // One placement for the whole take: monitor under the cursor at press.
            MonitorPlacement.PinNearCursor(this);
            ClipToPill();
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
        }));
    }

    private void ClipToPill()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 32, 32);
    }

    private void ConfigureNonActivating()
    {
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        const int gwlExStyle = -20;
        const int wsExToolWindow = 0x00000080;
        const int wsExNoActivate = 0x08000000;
        var ex = GetWindowLongPtr(hwnd, gwlExStyle).ToInt64();
        SetWindowLongPtr(hwnd, gwlExStyle, new IntPtr(ex | wsExToolWindow | wsExNoActivate));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
