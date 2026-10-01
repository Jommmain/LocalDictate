using System.Windows;
using System.Windows.Media;

namespace LocalDictate.Windows;

/// <summary>
/// Voice-style waveform. Silence keeps a small breathing motion instead of a flat strip.
/// </summary>
public sealed class WaveformControl : FrameworkElement
{
    public static readonly DependencyProperty LevelProperty = DependencyProperty.Register(
        nameof(Level),
        typeof(double),
        typeof(WaveformControl),
        new FrameworkPropertyMetadata(0.0));

    public static readonly DependencyProperty IsLiveProperty = DependencyProperty.Register(
        nameof(IsLive),
        typeof(bool),
        typeof(WaveformControl),
        new FrameworkPropertyMetadata(false));

    private const int BarCount = 28;
    private readonly double[] _bars = new double[BarCount];
    private readonly double[] _targets = new double[BarCount];
    private double _phase;
    private TimeSpan _lastTime;
    private TimeSpan _lastSample;
    private bool _hooked;

    private static readonly System.Windows.Media.Brush BarBrush = CreateBarBrush();

    public WaveformControl()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        for (var i = 0; i < BarCount; i++)
        {
            _targets[i] = 0.10 + 0.04 * Math.Sin(i * 0.55);
            _bars[i] = _targets[i];
        }

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) Hook();
            else Unhook();
        };
    }

    public double Level
    {
        get => (double)GetValue(LevelProperty);
        set => SetValue(LevelProperty, value);
    }

    public bool IsLive
    {
        get => (bool)GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        const double gap = 3;
        var barWidth = Math.Max(2, (ActualWidth - gap * (BarCount - 1)) / BarCount);
        var mid = ActualHeight / 2;
        for (var i = 0; i < BarCount; i++)
        {
            var height = Math.Max(3, _bars[i] * (ActualHeight - 2));
            var x = i * (barWidth + gap);
            var y = mid - height / 2;
            dc.DrawRoundedRectangle(BarBrush, null, new Rect(x, y, barWidth, height), barWidth / 2, barWidth / 2);
        }
    }

    private void Hook()
    {
        if (_hooked || !IsVisible)
        {
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        _hooked = true;
    }

    private void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _hooked = false;
        _lastTime = TimeSpan.Zero;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs args)
        {
            return;
        }

        var now = args.RenderingTime;
        var dt = _lastTime == TimeSpan.Zero ? 0.016 : (now - _lastTime).TotalSeconds;
        _lastTime = now;
        if (dt > 0.1) dt = 0.1;
        _phase += dt;

        if ((now - _lastSample).TotalMilliseconds >= 48)
        {
            _lastSample = now;
            PushSample();
        }

        for (var i = 0; i < BarCount; i++)
        {
            _bars[i] += (_targets[i] - _bars[i]) * 0.32;
        }

        InvalidateVisual();
    }

    private void PushSample()
    {
        for (var i = 0; i < BarCount - 1; i++)
        {
            _targets[i] = _targets[i + 1];
        }

        var idle = 0.10 + 0.05 * Math.Sin(_phase * 2.1);
        var level = Math.Clamp(Level, 0, 1);
        var sample = !IsLive || level < 0.035
            ? idle
            : Math.Clamp(idle * 0.25 + level * (0.70 + 0.30 * Math.Abs(Math.Sin(_phase * 6.2))), 0.08, 1);
        _targets[^1] = sample;
    }

    private static System.Windows.Media.Brush CreateBarBrush()
    {
        var brush = new LinearGradientBrush(
            Color.FromRgb(0x1F, 0x74, 0xE0),
            Color.FromRgb(0x12, 0xB5, 0xC4),
            new Point(0.5, 1),
            new Point(0.5, 0));
        brush.Freeze();
        return brush;
    }
}
