using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace MediaPlayer.Helpers;

public sealed class AnimatedAppBackground : Grid
{
    private readonly LinearGradientBrush _brush = new();
    private readonly GradientStop[] _stops;
    private readonly DispatcherTimer _timer;

    private double _phase;
    private double _direction = 1;
    private bool _animationPaused;

    private static readonly (double Offset, byte R, byte G, byte B)[] PaletteA =
    [
        (0.00, 10, 14, 28),
        (0.22, 12, 16, 34),
        (0.48, 15, 17, 40),
        (0.72, 18, 17, 46),
        (1.00, 22, 18, 52),
    ];

    private static readonly (double Offset, byte R, byte G, byte B)[] PaletteB =
    [
        (0.00, 12, 16, 34),
        (0.22, 14, 18, 40),
        (0.48, 17, 19, 46),
        (0.72, 20, 19, 52),
        (1.00, 24, 20, 58),
    ];

    public AnimatedAppBackground()
    {
        _brush.StartPoint = new Point(0, 0);
        _brush.EndPoint = new Point(1, 1);
        _brush.MappingMode = BrushMappingMode.RelativeToBoundingBox;

        _stops = PaletteA.Select(p => new GradientStop
        {
            Offset = p.Offset,
            Color = Color.FromArgb(255, p.R, p.G, p.B)
        }).ToArray();

        foreach (var stop in _stops)
            _brush.GradientStops.Add(stop);

        Background = _brush;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += OnTick;
        Loaded += (_, _) =>
        {
            if (!_animationPaused)
                _timer.Start();
        };
        Unloaded += (_, _) => _timer.Stop();
    }

    public void SetAnimationPaused(bool paused)
    {
        if (_animationPaused == paused)
            return;

        _animationPaused = paused;
        if (paused)
            _timer.Stop();
        else if (IsLoaded)
            _timer.Start();
    }

    private void OnTick(object? sender, object e)
    {
        const double step = 0.004;

        _phase += step * _direction;
        if (_phase >= 1)
        {
            _phase = 1;
            _direction = -1;
        }
        else if (_phase <= 0)
        {
            _phase = 0;
            _direction = 1;
        }

        ApplyPalette(_phase);
    }

    private void ApplyPalette(double t)
    {
        for (var i = 0; i < _stops.Length; i++)
        {
            var a = PaletteA[i];
            var b = PaletteB[i];
            _stops[i].Color = Color.FromArgb(
                255,
                Lerp(a.R, b.R, t),
                Lerp(a.G, b.G, t),
                Lerp(a.B, b.B, t));
        }
    }

    private static byte Lerp(byte from, byte to, double t)
        => (byte)Math.Clamp(from + (to - from) * t, 0, 255);
}
