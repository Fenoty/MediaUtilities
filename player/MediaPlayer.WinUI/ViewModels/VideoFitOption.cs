using MediaPlayer.Helpers;

namespace MediaPlayer.ViewModels;

public sealed class VideoFitOption(VideoFitMode mode, string label)
{
    public VideoFitMode Mode { get; } = mode;
    public string Label { get; } = label;
}
