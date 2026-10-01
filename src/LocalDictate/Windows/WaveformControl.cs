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

    public static readonly DependencyProperty ColorfulProperty = DependencyProperty.Register(
        nameof(Colorful),
        typeof(bool),
        typeof(WaveformControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private const int BarCount = 28;
    private readonly double[] _bars = new double[BarCount];
    private readonly double[] _targets = new double[BarCount];
    private double _phase;
    private TimeSpan _lastTime;
    private TimeSpan _lastSample;
    private bool _hooked;

    private static readonly System.Windows.Media.Brush BarBrush = CreateBarBrush();
    private readonly System.Windows.Media.Brush[] _spectrum = CreateSpectrum();

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

    public bool Colorful
    {
        get => (bool)GetValue(ColorfulProperty);
        set => SetValue(ColorfulProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 1 || ActualHeight <= 1)
        {
            return;
        }

        const double gap = 2.5;
        var barWidth = Math.Max(2.5, (ActualWidth - gap * (BarCount - 1)) / BarCount);
        var mid = ActualHeight / 2;
        for (var i = 0; i < BarCount; i++)
        {
            var height = Math.Max(4, _bars[i] * (ActualHeight - 2));
            var x = i * (barWidth + gap);
            var y = mid - height / 2;
            var brush = Colorful ? _spectrum[i] : BarBrush;
            dc.DrawRoundedRectangle(brush, null, new Rect(x, y, barWidth, height), barWidth / 2, barWidth / 2);
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

    private static System.Windows.Media.Brush[] CreateSpectrum()
    {
        // One hue per bar, blue through magenta, amber, and teal. Not a flat strip.
        ReadOnlySpan<uint> hues =
        [
            0x3B82F6, 0x4F46E5, 0x7C3AED, 0xA855F7, 0xD946EF, 0xEC4899,
            0xF43F5E, 0xF97316, 0xF59E0B, 0xEAB308, 0x84CC16, 0x22C55E,
            0x14B8A6, 0x06B6D4, 0x0EA5E9, 0x3B82F6, 0x6366F1, 0x8B5CF6,
            0xC026D3, 0xDB2777, 0xE11D48, 0xEA580C, 0xD97706, 0x65A30D,
            0x059669, 0x0D9488, 0x0891B2, 0x0284C7,
        ];
        var brushes = new System.Windows.Media.Brush[BarCount];
        for (var i = 0; i < BarCount; i++)
        {
            var packed = hues[i % hues.Length];
            var bottom = Color.FromRgb(
                (byte)((packed >> 16) & 0xFF),
                (byte)((packed >> 8) & 0xFF),
                (byte)(packed & 0xFF));
            var top = Color.FromRgb(
                (byte)Math.Min(255d, bottom.R + (255 - bottom.R) * 0.42),
                (byte)Math.Min(255d, bottom.G + (255 - bottom.G) * 0.42),
                (byte)Math.Min(255d, bottom.B + (255 - bottom.B) * 0.42));
            var brush = new LinearGradientBrush(top, bottom, new Point(0.5, 0), new Point(0.5, 1));
            brush.Freeze();
            brushes[i] = brush;
        }

        return brushes;
    }
}
