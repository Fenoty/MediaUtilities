using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using MediaPlayer.Helpers;
using MediaPlayer.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using Windows.System;

namespace MediaPlayer.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private const int MetadataPrefetchCount = 12;

    private readonly PlaybackService _playback;
    private readonly PlaylistService _playlist;
    private readonly SettingsService _settingsService;
    private readonly ThumbnailService _thumbnailService;
    private readonly AppSettings _settings;
    private readonly CancellationTokenSource _metadataCts = new();

    private bool _isSeeking;
    private bool _disposed;
    private bool _autoPlayRequested;
    private bool _syncingVolumeSlider;
    private string? _currentFilePath;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private int _volume = 50;

    [ObservableProperty]
    private double _position;

    [ObservableProperty]
    private double _maximum = 1;

    [ObservableProperty]
    private TimeSpan _currentTime;

    [ObservableProperty]
    private TimeSpan _totalTime;

    [ObservableProperty]
    private float _playbackRate = 1f;

    [ObservableProperty]
    private PlaybackRateItem? _selectedPlaybackRate;

    public IReadOnlyList<PlaybackRateItem> PlaybackRateOptions { get; } =
    [
        new(0.25f), new(0.5f), new(0.75f), new(1f),
        new(1.25f), new(1.5f), new(1.75f), new(2f)
    ];

    [ObservableProperty]
    private bool _isPlaylistVisible;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _hasSubtitles;

    [ObservableProperty]
    private bool _subtitlesEnabled;

    [ObservableProperty]
    private string _windowTitle = AppVersion.DefaultWindowTitle;

    [ObservableProperty]
    private bool _hasActiveMedia;

    [ObservableProperty]
    private bool _isAudioMode;

    [ObservableProperty]
    private ImageSource? _audioArtwork;

    [ObservableProperty]
    private string _audioTitle = string.Empty;

    [ObservableProperty]
    private int _selectedPlaylistIndex = -1;

    [ObservableProperty]
    private VideoFitOption? _selectedVideoFit;

    [ObservableProperty]
    private bool _lockWindowToVideoAspect;

    [ObservableProperty]
    private RepeatMode _repeatMode = RepeatMode.Off;

    public static IReadOnlyList<VideoFitOption> VideoFitOptions { get; } =
    [
        new(VideoFitMode.Fit, "Fit to screen"),
        new(VideoFitMode.Fill, "Fill screen"),
        new(VideoFitMode.Stretch, "Stretch to full screen")
    ];

    public event Action? VideoFitChanged;

    public ObservableCollection<PlaylistItemViewModel> PlaylistItems { get; } = [];
    public ObservableCollection<string> RecentFiles { get; } = [];

    public VlcMediaPlayer? LibVlcMediaPlayer => _playback.MediaPlayer;
    public bool IsPlaybackReady => _playback.IsInitialized;
    public VideoZoomController? VideoZoom => _playback.VideoZoom;
    public uint VideoWidth => _playback.VideoFit.VideoWidth;
    public uint VideoHeight => _playback.VideoFit.VideoHeight;

    public bool IsVideoMode => HasActiveMedia && !IsAudioMode;

    public string CurrentTimeText => TimeFormat.Format(CurrentTime);
    public string TotalTimeText => TimeFormat.Format(TotalTime);

    public bool IsRepeatOff => RepeatMode == RepeatMode.Off;
    public bool IsRepeatAll => RepeatMode == RepeatMode.All;
    public bool IsRepeatOne => RepeatMode == RepeatMode.One;

    public string RepeatModeTooltip => RepeatMode switch
    {
        RepeatMode.Off => "No repeat",
        RepeatMode.All => "Repeat playlist",
        RepeatMode.One => "Repeat current file",
        _ => "Repeat mode"
    };

    public int VolumeSliderValue
    {
        get => IsMuted ? 0 : Volume;
        set
        {
            if (_syncingVolumeSlider)
                return;

            Volume = Math.Clamp(value, 0, 100);
            if (IsMuted)
                IsMuted = false;
        }
    }

    public event Action? RequestToggleFullscreen;
    public event Action? RequestExitFullscreen;
    public event Action<string>? ShowError;
    public event Action<string, bool>? RequestStartPlayback;

    public MainViewModel()
    {
        _playback = new PlaybackService();
        _playlist = new PlaylistService();
        _settingsService = new SettingsService();
        _thumbnailService = new ThumbnailService();
        _settings = _settingsService.Load();

        Volume = _settings.Volume;
        IsPlaylistVisible = false;
        LockWindowToVideoAspect = _settings.LockWindowToVideoAspect;
        SelectedVideoFit = GetVideoFitOption(ParseVideoFitMode(_settings.VideoFitMode));
        RepeatMode = ParseRepeatMode(_settings.RepeatMode);
        foreach (var file in _settings.RecentFiles.Where(File.Exists))
            RecentFiles.Add(file);

        _playback.TimeChanged += OnPlaybackTimeChanged;
        _playback.PlaybackEnded += OnPlaybackEnded;
        _playback.PlaybackError += msg => ShowError?.Invoke(msg);
        _playback.StateChanged += OnPlaybackStateChanged;
        _playback.DurationAvailable += OnDurationAvailable;
        _playback.VideoFitNeedsApply += () => VideoFitChanged?.Invoke();
        _playback.MediaCharacteristicsChanged += OnMediaCharacteristicsChanged;

        _playlist.ItemSelected += PlayFile;
        _playlist.PlaylistChanged += OnPlaylistChanged;

        SyncPlaylistItems();
        SelectedPlaybackRate = PlaybackRateOptions.First(r => Math.Abs(r.Rate - 1f) < 0.01f);
    }

    public void InitializePlayback(InitializedEventArgs eventArgs)
    {
        if (_playback.IsInitialized)
            return;

        try
        {
            _playback.Initialize(eventArgs.SwapChainOptions);
            _playback.SetVolume(Volume);
            _playback.SetMute(IsMuted);
            _playback.VideoFit.Mode = SelectedVideoFit?.Mode ?? VideoFitMode.Fit;
            OnPropertyChanged(nameof(LibVlcMediaPlayer));
            OnPropertyChanged(nameof(IsPlaybackReady));
            OnPropertyChanged(nameof(VideoZoom));
        }
        catch (Exception ex)
        {
            ShowError?.Invoke($"Video initialization error: {ex.Message}");
        }
    }

    public void ApplyWindowSettings(Window window)
    {
        if (window.AppWindow is null)
            return;

        var width = (int)Helpers.AdaptiveLayout.ClampWindowDimension(
            _settings.WindowWidth, Helpers.AdaptiveLayout.MinWindowWidth);
        var height = (int)Helpers.AdaptiveLayout.ClampWindowDimension(
            _settings.WindowHeight, Helpers.AdaptiveLayout.MinWindowHeight);

        window.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    public void SaveSettings(Window window)
    {
        _settings.Volume = Volume;
        if (window.AppWindow is not null)
        {
            var size = window.AppWindow.Size;
            _settings.WindowWidth = size.Width;
            _settings.WindowHeight = size.Height;
        }

        _settings.VideoFitMode = (SelectedVideoFit?.Mode ?? VideoFitMode.Fit).ToString();
        _settings.LockWindowToVideoAspect = LockWindowToVideoAspect;
        _settings.RepeatMode = RepeatMode.ToString();
        _settings.RecentFiles = RecentFiles.ToList();
        _settingsService.Save(_settings);
    }

    partial void OnIsPlayingChanged(bool value) => UpdatePlayingIndicators();

    partial void OnCurrentTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(CurrentTimeText));
    }

    partial void OnTotalTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(TotalTimeText));
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        try
        {
            var files = await FilePickerService.PickMediaAsync();
            if (files.Count > 0)
                OpenFiles(files);
        }
        catch (Exception ex)
        {
            ShowError?.Invoke(ex.Message);
        }
    }

    public Task PromptOpenFileAsync() => OpenFileAsync();

    public void OpenFiles(IEnumerable<string> paths)
    {
        var list = paths.Where(p => File.Exists(p) && MediaFileHelper.IsMediaFile(p)).ToList();
        if (list.Count == 0)
            return;

        _playlist.AddFiles(list);
        _playlist.PlayItem(list[0]);
    }

    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            ShowError?.Invoke("File not found.");
            RecentFiles.Remove(path!);
            return;
        }

        _playlist.PlayItem(path);
    }

    [RelayCommand]
    private async Task PlayPauseAsync()
    {
        if (_currentFilePath == null)
        {
            await OpenFileAsync();
            return;
        }

        _playback.PlayPause();
    }

    [RelayCommand]
    private void Previous()
    {
        _autoPlayRequested = true;
        _playlist.PlayPrevious();
    }

    [RelayCommand]
    private void Next()
    {
        _autoPlayRequested = true;
        _playlist.PlayNext();
    }

    [RelayCommand]
    private void SeekBackward10()
    {
        if (HasActiveMedia)
            _playback.SeekRelative(-10_000);
    }

    [RelayCommand]
    private void SeekForward10()
    {
        if (HasActiveMedia)
            _playback.SeekRelative(10_000);
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void ToggleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.Off => RepeatMode.All,
            RepeatMode.All => RepeatMode.One,
            _ => RepeatMode.Off
        };
    }

    [RelayCommand]
    private void TogglePlaylist() => IsPlaylistVisible = !IsPlaylistVisible;

    [RelayCommand]
    private void ToggleFullscreen()
    {
        if (IsFullscreen)
            RequestExitFullscreen?.Invoke();
        else
            RequestToggleFullscreen?.Invoke();
    }

    [RelayCommand]
    private async Task LoadSubtitlesAsync()
    {
        var path = await FilePickerService.PickSubtitleAsync();
        if (path is null)
            return;

        _playback.LoadSubtitle(path);
        HasSubtitles = true;
        SubtitlesEnabled = true;
    }

    [RelayCommand]
    private async Task ToggleSubtitlesAsync()
    {
        if (!HasSubtitles)
        {
            await LoadSubtitlesAsync();
            return;
        }

        SubtitlesEnabled = !SubtitlesEnabled;
        _playback.ToggleSubtitles(SubtitlesEnabled);
    }

    [RelayCommand]
    private async Task AddToPlaylistAsync()
    {
        try
        {
            var files = await FilePickerService.PickMediaAsync(
                title: "Select one or more media files");
            var list = files.Where(p => File.Exists(p) && MediaFileHelper.IsMediaFile(p)).ToList();
            if (list.Count > 0)
                _playlist.AddFiles(list);
        }
        catch (Exception ex)
        {
            ShowError?.Invoke(ex.Message);
        }
    }

    public Task PromptAddToPlaylistAsync() => AddToPlaylistAsync();

    [RelayCommand]
    private void PlayPlaylistItem(PlaylistItemViewModel? item)
    {
        if (item != null)
            _playlist.PlayItem(item.FilePath);
    }

    public async void TogglePlayPauseFromVideo()
    {
        try
        {
            if (!HasActiveMedia)
                await OpenFileAsync();
            else
                _playback.PlayPause();
        }
        catch (Exception ex)
        {
            ShowError?.Invoke(ex.Message);
        }
    }

    public void BeginSeek() => _isSeeking = true;

    public void PreviewSeek(double value)
    {
        if (!_isSeeking || Maximum <= 0)
            return;

        CurrentTime = TimeSpan.FromMilliseconds(Math.Clamp(value, 0, Maximum));
    }

    public void CommitSeek(double value)
    {
        if (Maximum <= 0)
        {
            _isSeeking = false;
            return;
        }

        var ms = (long)Math.Clamp(value, 0, Maximum);
        Position = ms;
        CurrentTime = TimeSpan.FromMilliseconds(ms);
        _playback.SeekTo(ms);
        _isSeeking = false;
    }

    private void OnDurationAvailable(long durationMs)
    {
        Maximum = durationMs > 0 ? durationMs : 1;
        TotalTime = TimeSpan.FromMilliseconds(Math.Max(durationMs, 0));
    }

    partial void OnVolumeChanged(int value)
    {
        if (_disposed)
            return;

        if (IsMuted && value > 0)
            IsMuted = false;

        if (!IsMuted)
            _playback.SetVolume(value);

        if (!_syncingVolumeSlider)
            OnPropertyChanged(nameof(VolumeSliderValue));
    }

    partial void OnIsMutedChanged(bool value)
    {
        _playback.SetMute(value);
        if (!value)
            _playback.SetVolume(Volume);

        _syncingVolumeSlider = true;
        OnPropertyChanged(nameof(VolumeSliderValue));
        _syncingVolumeSlider = false;
    }

    private void PlayFile(string path)
    {
        UiDispatcher.Invoke(() => PlayFileOnUi(path));
    }

    private void PlayFileOnUi(string path)
    {
        try
        {
            _currentFilePath = path;
            HasActiveMedia = true;
            IsAudioMode = MediaFileHelper.IsAudioFile(path);
            AudioTitle = Path.GetFileNameWithoutExtension(path);
            AudioArtwork = null;
            WindowTitle = Path.GetFileName(path);
            OnPropertyChanged(nameof(IsVideoMode));

            if (IsAudioMode)
                _ = LoadAudioArtworkAsync(path);

            _settingsService.AddRecentFile(_settings, path);
            if (!RecentFiles.Contains(path))
            {
                RecentFiles.Insert(0, path);
                while (RecentFiles.Count > 10)
                    RecentFiles.RemoveAt(RecentFiles.Count - 1);
            }

            SelectedPlaylistIndex = _playlist.CurrentIndex;
            UpdatePlayingIndicators();

            if (IsPlaylistVisible)
            {
                var item = PlaylistItems.ElementAtOrDefault(_playlist.CurrentIndex);
                _ = DeferLoadMetadataAsync(item);
            }

            RequestStartPlayback?.Invoke(path, _autoPlayRequested);
            _autoPlayRequested = false;
        }
        catch (Exception ex)
        {
            ShowError?.Invoke($"Playback error: {ex.Message}");
        }
    }

    public void LoadMedia(string path, bool autoPlay)
    {
        if (!_playback.IsInitialized)
        {
            ShowError?.Invoke("Video surface is not ready yet.");
            return;
        }

        try
        {
            _playback.Load(path, autoPlay);
            UpdatePositionFromPlayer();
            IsPlaying = _playback.IsPlaying;
        }
        catch (Exception ex)
        {
            ShowError?.Invoke($"Playback error: {ex.Message}");
        }
    }

    private void OnMediaCharacteristicsChanged()
    {
        UiDispatcher.Invoke(() =>
        {
            IsAudioMode = _playback.IsAudioOnly;
            OnPropertyChanged(nameof(IsVideoMode));
            OnPropertyChanged(nameof(VideoWidth));
            OnPropertyChanged(nameof(VideoHeight));
            VideoFitChanged?.Invoke();
        });
    }

    partial void OnLockWindowToVideoAspectChanged(bool value)
    {
        _settings.LockWindowToVideoAspect = value;

        if (!value || !IsVideoMode)
            return;

        var fitMode = VideoFitOptions.First(o => o.Mode == VideoFitMode.Fit);
        if (SelectedVideoFit?.Mode != VideoFitMode.Fit)
            SelectedVideoFit = fitMode;
    }

    private async Task LoadAudioArtworkAsync(string path)
    {
        try
        {
            var thumbnail = await ThumbnailService.LoadFileThumbnailAsync(path);
            if (_currentFilePath != path)
                return;

            App.DispatcherQueue.TryEnqueue(() => AudioArtwork = thumbnail);
        }
        catch
        {
            // optional artwork
        }
    }

    private async Task DeferLoadMetadataAsync(PlaylistItemViewModel? item)
    {
        await Task.Delay(2000);
        if (item is null || item.IsMetadataLoaded)
            return;

        try
        {
            var duration = await _thumbnailService.LoadDurationAsync(
                item.FilePath,
                _metadataCts.Token);
            var thumbnail = await ThumbnailService.LoadFileThumbnailAsync(item.FilePath);

            App.DispatcherQueue.TryEnqueue(() =>
            {
                var resolvedDuration = TotalTime > TimeSpan.Zero ? TotalTime : duration;
                item.ApplyMetadata(resolvedDuration, thumbnail);
            });
        }
        catch
        {
            // optional
        }
    }

    private void OnPlaybackEnded()
    {
        _autoPlayRequested = true;

        var continued = RepeatMode switch
        {
            RepeatMode.Off => _playlist.PlayNext(loop: false),
            RepeatMode.All => _playlist.PlayNext(loop: true),
            RepeatMode.One => _playlist.ReplayCurrent(),
            _ => false
        };

        if (continued)
            return;

        _autoPlayRequested = false;
        IsPlaying = false;
        Position = Maximum;
        CurrentTime = TotalTime;
    }

    private void OnPlaybackStateChanged()
    {
        IsPlaying = _playback.IsPlaying;
        SyncSelectedPlaybackRate(_playback.Rate);
    }

    private void OnPlaybackTimeChanged()
    {
        if (!_isSeeking)
            UpdatePositionFromPlayer();
    }

    private void UpdatePositionFromPlayer()
    {
        var length = _playback.LengthMs;
        Maximum = length > 0 ? length : 1;
        TotalTime = TimeSpan.FromMilliseconds(Math.Max(length, 0));

        if (!_isSeeking)
        {
            Position = _playback.TimeMs;
            CurrentTime = TimeSpan.FromMilliseconds(_playback.TimeMs);
        }

        IsPlaying = _playback.IsPlaying;
    }

    partial void OnSelectedVideoFitChanged(VideoFitOption? value)
    {
        if (value is null)
            return;

        if (LockWindowToVideoAspect && value.Mode != VideoFitMode.Fit)
        {
            SelectedVideoFit = VideoFitOptions.First(o => o.Mode == VideoFitMode.Fit);
            return;
        }

        _playback.VideoFit.Mode = value.Mode;
        _settings.VideoFitMode = value.Mode.ToString();
        VideoZoom?.Reset();
        VideoFitChanged?.Invoke();
    }

    partial void OnRepeatModeChanged(RepeatMode value)
    {
        _settings.RepeatMode = value.ToString();
        OnPropertyChanged(nameof(IsRepeatOff));
        OnPropertyChanged(nameof(IsRepeatAll));
        OnPropertyChanged(nameof(IsRepeatOne));
        OnPropertyChanged(nameof(RepeatModeTooltip));
    }

    public void ApplyVideoFit(double viewWidth, double viewHeight)
        => _playback.ApplyVideoFit(viewWidth, viewHeight);

    private static VideoFitMode ParseVideoFitMode(string? value)
        => Enum.TryParse<VideoFitMode>(value, out var mode) ? mode : VideoFitMode.Fit;

    private static RepeatMode ParseRepeatMode(string? value)
        => Enum.TryParse<RepeatMode>(value, out var mode) ? mode : RepeatMode.Off;

    private static VideoFitOption GetVideoFitOption(VideoFitMode mode)
        => VideoFitOptions.FirstOrDefault(o => o.Mode == mode)
           ?? VideoFitOptions.First(o => o.Mode == VideoFitMode.Fit);

    partial void OnSelectedPlaybackRateChanged(PlaybackRateItem? value)
    {
        if (_disposed || value is null)
            return;

        ApplyPlaybackRate(value.Rate);
    }

    private void SyncSelectedPlaybackRate(float rate)
    {
        var item = PlaybackRateOptions.FirstOrDefault(r => Math.Abs(r.Rate - rate) < 0.01f);
        if (item is null)
            return;

        if (SelectedPlaybackRate?.Rate != item.Rate)
            SelectedPlaybackRate = item;
        else
            PlaybackRate = rate;
    }

    private void ApplyPlaybackRate(float rate)
    {
        PlaybackRate = rate;
        _playback.SetRate(rate);
    }

    private void OnPlaylistChanged()
    {
        SyncPlaylistItems();
        QueueMetadataLoading();
    }

    private void SyncPlaylistItems()
    {
        PlaylistItems.Clear();
        foreach (var path in _playlist.Items)
            PlaylistItems.Add(new PlaylistItemViewModel(path));

        SelectedPlaylistIndex = _playlist.CurrentIndex;
        UpdatePlayingIndicators();
    }

    partial void OnIsPlaylistVisibleChanged(bool value)
    {
        if (value)
            QueueMetadataLoading();
    }

    private void QueueMetadataLoading()
    {
        if (!IsPlaylistVisible)
            return;

        var count = 0;
        foreach (var item in PlaylistItems)
        {
            if (item.IsMetadataLoaded)
                continue;

            _ = LoadMetadataForItemAsync(item);
            if (++count >= MetadataPrefetchCount)
                break;
        }
    }

    public void RequestPlaylistItemMetadata(int index)
    {
        if (!IsPlaylistVisible || index < 0 || index >= PlaylistItems.Count)
            return;

        var item = PlaylistItems[index];
        if (!item.IsMetadataLoaded)
            _ = LoadMetadataForItemAsync(item);
    }

    private async Task LoadMetadataForItemAsync(PlaylistItemViewModel? item)
    {
        if (item == null || item.IsMetadataLoaded)
            return;

        try
        {
            var duration = await _thumbnailService.LoadDurationAsync(
                item.FilePath,
                _metadataCts.Token);
            var thumbnail = await ThumbnailService.LoadFileThumbnailAsync(item.FilePath);

            App.DispatcherQueue.TryEnqueue(() =>
                item.ApplyMetadata(duration, thumbnail));
        }
        catch
        {
            App.DispatcherQueue.TryEnqueue(() => item.ApplyMetadata(TimeSpan.Zero, null));
        }
    }

    private void UpdatePlayingIndicators()
    {
        for (var i = 0; i < PlaylistItems.Count; i++)
        {
            PlaylistItems[i].IsPlaying = i == _playlist.CurrentIndex && IsPlaying;
            PlaylistItems[i].IsSelected = i == _playlist.CurrentIndex;
        }
    }

    public void HandleKey(VirtualKey key, bool ctrl)
    {
        switch (key)
        {
            case VirtualKey.Space:
                PlayPauseCommand.Execute(null);
                break;
            case VirtualKey.F:
                ToggleFullscreenCommand.Execute(null);
                break;
            case VirtualKey.Left:
                _playback.SeekRelative(-10_000);
                break;
            case VirtualKey.Right:
                _playback.SeekRelative(10_000);
                break;
            case VirtualKey.Up:
                Volume = Math.Min(100, Volume + 5);
                break;
            case VirtualKey.Down:
                Volume = Math.Max(0, Volume - 5);
                break;
            case VirtualKey.M:
                ToggleMuteCommand.Execute(null);
                break;
            case VirtualKey.O when ctrl:
                OpenFileCommand.Execute(null);
                break;
            case VirtualKey.L when ctrl:
                LoadSubtitlesCommand.Execute(null);
                break;
            case VirtualKey.N:
                NextCommand.Execute(null);
                break;
            case VirtualKey.P:
                PreviousCommand.Execute(null);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _metadataCts.Cancel();
        _metadataCts.Dispose();
        _playback.Dispose();
        VlcAuxiliaryService.Dispose();
    }
}
