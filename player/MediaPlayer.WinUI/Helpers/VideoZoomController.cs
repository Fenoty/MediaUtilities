namespace MediaPlayer.Helpers;

public sealed class VideoZoomController
{
    private double _scale = 1.0;

    public const double MinZoom = 1.0;
    public const double MaxZoom = 2.5;
    private const double ZoomStep = 1.06;

    public double Scale => _scale;
    public double FocalX { get; private set; } = 0.5;
    public double FocalY { get; private set; } = 0.5;
    public bool IsZoomed => _scale > MinZoom + 0.001;

    public void Reset()
    {
        _scale = 1.0;
        FocalX = 0.5;
        FocalY = 0.5;
    }

    public bool AdjustAt(
        double viewWidth,
        double viewHeight,
        double pointerX,
        double pointerY,
        VideoFitMode fitMode,
        double videoWidth,
        double videoHeight,
        int wheelDelta)
    {
        if (wheelDelta == 0)
            return false;

        if (viewWidth > 0 && viewHeight > 0 && videoWidth > 0 && videoHeight > 0)
        {
            var rect = VideoLayoutHelper.GetContentRect(
                viewWidth, viewHeight, videoWidth, videoHeight, fitMode);

            if (rect.Width > 0 && rect.Height > 0)
            {
                FocalX = Math.Clamp((pointerX - rect.X) / rect.Width, 0, 1);
                FocalY = Math.Clamp((pointerY - rect.Y) / rect.Height, 0, 1);
            }
        }

        return Adjust(wheelDelta);
    }

    public bool Adjust(int wheelDelta)
    {
        if (wheelDelta == 0)
            return false;

        var steps = Math.Clamp(Math.Abs(wheelDelta) / 120.0, 1.0, 3.0);
        var factor = wheelDelta > 0 ? Math.Pow(ZoomStep, steps) : 1.0 / Math.Pow(ZoomStep, steps);
        var newScale = Math.Clamp(_scale * factor, MinZoom, MaxZoom);

        if (newScale <= MinZoom + 0.001)
        {
            Reset();
            return true;
        }

        _scale = newScale;
        return true;
    }
}
