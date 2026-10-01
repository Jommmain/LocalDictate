using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LocalDictate.Services;

namespace LocalDictate.Windows;

public partial class RecordingOverlay : Window
{
    private bool _pinned;
    private int _pinGeneration;

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

        PhaseText.Text = string.IsNullOrWhiteSpace(visual.PhaseLabel) ? "Запись" : visual.PhaseLabel;
        Wave.Level = visual.Level;
        Wave.IsLive = true;
        PulseDot.Fill = TryFindResource("AccentBrush") as Brush ?? PulseDot.Fill;

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

        Clip = new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight), 22, 22);
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
