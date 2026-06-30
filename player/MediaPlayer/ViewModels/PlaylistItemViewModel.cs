using CommunityToolkit.Mvvm.ComponentModel;
using MediaPlayer.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MediaPlayer.ViewModels;

public partial class PlaylistItemViewModel : ObservableObject
{
    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private BitmapImage? _thumbnail;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private bool _isMetadataLoaded;

    public string DurationText =>
        Duration > TimeSpan.Zero ? TimeFormat.Format(Duration) : "--:--";

    public PlaylistItemViewModel(string filePath)
    {
        FilePath = filePath;
    }

    public void ApplyMetadata(TimeSpan duration, BitmapImage? thumbnail)
    {
        Duration = duration;
        Thumbnail = thumbnail;
        IsMetadataLoaded = true;
        OnPropertyChanged(nameof(DurationText));
    }
}
