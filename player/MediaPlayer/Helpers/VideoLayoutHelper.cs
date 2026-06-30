using Windows.Foundation;

namespace MediaPlayer.Helpers;

public static class VideoLayoutHelper
{
    public static Rect GetContentRect(
        double viewWidth,
        double viewHeight,
        double videoWidth,
        double videoHeight,
        VideoFitMode mode)
    {
        if (viewWidth <= 0 || viewHeight <= 0 || videoWidth <= 0 || videoHeight <= 0)
            return new Rect(0, 0, viewWidth, viewHeight);

        return mode switch
        {
            VideoFitMode.Fit => GetFitContentRect(viewWidth, viewHeight, videoWidth, videoHeight),
            _ => new Rect(0, 0, viewWidth, viewHeight)
        };
    }

    private static Rect GetFitContentRect(
        double viewWidth,
        double viewHeight,
        double videoWidth,
        double videoHeight)
    {
        var scale = Math.Min(viewWidth / videoWidth, viewHeight / videoHeight);
        var contentWidth = videoWidth * scale;
        var contentHeight = videoHeight * scale;
        return new Rect(
            (viewWidth - contentWidth) / 2,
            (viewHeight - contentHeight) / 2,
            contentWidth,
            contentHeight);
    }
}
