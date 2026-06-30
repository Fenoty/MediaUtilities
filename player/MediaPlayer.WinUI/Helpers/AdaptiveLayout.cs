namespace MediaPlayer.Helpers;

public static class AdaptiveLayout
{
    public const int MinWindowWidth = 720;
    public const int MinWindowHeight = 420;
    public const double BasePlaylistWidth = 360;
    public const double BasePlaylistMinWidth = 240;

    public const double NarrowBreakpoint = 0;
    public const double MinimumBreakpoint = 400;
    public const double MenuWideBreakpoint = 480;
    public const double WideBreakpoint = 720;

    public static double ClampWindowDimension(double value, int minimum)
        => value < minimum ? minimum : value;

    public static double GetPlaylistBaseWidth(double windowWidth)
    {
        if (windowWidth >= 900)
            return BasePlaylistWidth;

        return Math.Clamp(
            windowWidth - 56,
            BasePlaylistMinWidth,
            BasePlaylistWidth);
    }

    public static string GetWindowWidthVisualState(double windowWidth)
    {
        if (windowWidth >= WideBreakpoint)
            return "Wide";
        if (windowWidth >= MenuWideBreakpoint)
            return "MenuWide";
        if (windowWidth >= MinimumBreakpoint)
            return "Minimum";
        return "Narrow";
    }
}
