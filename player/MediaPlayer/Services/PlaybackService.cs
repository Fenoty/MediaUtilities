using LibVLCSharp.Shared;
using MediaPlayer.Helpers;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace MediaPlayer.Services;

public class PlaybackService : IDisposable
{
    private LibVLC? _libVlc;
    private VlcMediaPlayer? _mediaPlayer;
    private VlcMedia? _currentMedia;
    private string? _subtitlePath;
    private string? _currentFilePath;
    private bool _disposed;
    private bool _pendingPreviewFrame;
    private bool _isAudioOnly;
    private long _lastTimeChangedMs;
    private readonly EventHandler<MediaPlayerTimeChangedEventArgs> _timeChangedHandler;
    private readonly EventHandler<EventArgs> _endReachedHandler;
    private readonly EventHandler<EventArgs> _errorHandler;
    private readonly EventHandler<EventArgs> _playingHandler;
    private readonly EventHandler<EventArgs> _pausedHandler;
    private readonly EventHandler<EventArgs> _stoppedHandler;
    private readonly EventHandler<MediaPlayerVoutEventArgs> _voutHandler;

    public bool IsInitialized => _mediaPlayer is not null;
    public bool IsAudioOnly => _isAudioOnly;
    public VlcMediaPlayer? MediaPlayer => _mediaPlayer;
    public VideoZoomController? VideoZoom { get; private set; }
    public VideoFitController VideoFit { get; } = new();

    public event Action? TimeChanged;
    public event Action? PlaybackEnded;
    public event Action<string>? PlaybackError;
    public event Action? StateChanged;
    public event Action<long>? DurationAvailable;
    public event Action? VideoFitNeedsApply;
    public event Action? MediaCharacteristicsChanged;

    public string? CurrentFilePath => _currentFilePath;
    public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
    public long LengthMs => _mediaPlayer?.Length ?? 0;
    public long TimeMs => _mediaPlayer?.Time ?? 0;
    public float Rate => _mediaPlayer?.Rate ?? 1f;

    public PlaybackService()
    {
        _timeChangedHandler = OnTimeChanged;
        _endReachedHandler = OnEndReached;
        _errorHandler = OnEncounteredError;
        _playingHandler = (_, _) => InvokeOnUi(() =>
        {
            StateChanged?.Invoke();
            VideoFitNeedsApply?.Invoke();
        });
        _pausedHandler = (_, _) => InvokeOnUi(() => StateChanged?.Invoke());
        _stoppedHandler = (_, _) => InvokeOnUi(() => StateChanged?.Invoke());
        _voutHandler = (_, _) => InvokeOnUi(() =>
        {
            ShowPreviewFrameIfNeeded();
            VideoFitNeedsApply?.Invoke();
        });
    }

    public void Initialize(string[] swapChainOptions)
    {
        if (_mediaPlayer is not null)
            return;

        try
        {
            LibVlcBootstrap.EnsureInitialized();
            _libVlc = new LibVLC(enableDebugLogs: false, swapChainOptions);
            _mediaPlayer = new VlcMediaPlayer(_libVlc);
            VideoZoom = new VideoZoomController();
            VideoFit.Attach(_mediaPlayer);

            _mediaPlayer.TimeChanged += _timeChangedHandler;
            _mediaPlayer.EndReached += _endReachedHandler;
            _mediaPlayer.EncounteredError += _errorHandler;
            _mediaPlayer.Playing += _playingHandler;
            _mediaPlayer.Paused += _pausedHandler;
            _mediaPlayer.Stopped += _stoppedHandler;
            _mediaPlayer.Vout += _voutHandler;
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke($"Failed to initialize video engine: {ex.Message}");
        }
    }

    public void Load(string filePath, bool autoPlay = false)
        => UiDispatcher.Invoke(() => LoadInternal(filePath, autoPlay));

    public void Play(string filePath) => Load(filePath, autoPlay: true);

    public void ApplyVideoFit(double viewWidth, double viewHeight)
        => UiDispatcher.Invoke(() =>
        {
            var zoom = VideoZoom?.Scale ?? 1.0;
            var focalX = VideoZoom?.FocalX ?? 0.5;
            var focalY = VideoZoom?.FocalY ?? 0.5;
            VideoFit.Apply(viewWidth, viewHeight, zoom, focalX, focalY);
        });

    private void LoadInternal(string filePath, bool autoPlay)
    {
        if (_mediaPlayer is null)
        {
            PlaybackError?.Invoke("Video surface is not ready yet.");
            return;
        }

        try
        {
            StopInternal();
            VideoZoom?.Reset();
            _mediaPlayer.CropGeometry = null;
            _currentFilePath = filePath;

            _isAudioOnly = MediaFileHelper.IsAudioFile(filePath);

            _pendingPreviewFrame = !autoPlay && !_isAudioOnly;
            _currentMedia = new VlcMedia(_libVlc!, filePath, FromType.FromPath);
            _currentMedia.AddOption(":file-caching=800");
            if (!autoPlay)
                _currentMedia.AddOption(":start-paused");
            _currentMedia.ParsedChanged += OnMediaParsedChanged;

            _mediaPlayer.Play(_currentMedia);

            if (!string.IsNullOrEmpty(_subtitlePath) && !_isAudioOnly)
                ApplySubtitle(_subtitlePath);

            MediaCharacteristicsChanged?.Invoke();
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            PlaybackError?.Invoke($"Video load error: {ex.Message}");
        }
    }

    private void OnMediaParsedChanged(object? sender, MediaParsedChangedEventArgs e)
    {
        if (_currentMedia is null || e.ParsedStatus != MediaParsedStatus.Done)
            return;

        var duration = _currentMedia.Duration;
        UpdateVideoDimensions(_currentMedia);
        UpdateAudioOnlyFromTracks(_currentMedia);

        UiDispatcher.Invoke(() =>
        {
            if (duration > 0)
                DurationAvailable?.Invoke(duration);

            if (!_isAudioOnly)
                ShowPreviewFrameIfNeeded();

            MediaCharacteristicsChanged?.Invoke();
            VideoFitNeedsApply?.Invoke();
        });
    }

    private void UpdateAudioOnlyFromTracks(VlcMedia media)
    {
        var hasVideo = false;
        foreach (var track in media.Tracks)
        {
            if (track.TrackType != TrackType.Video)
                continue;

            hasVideo = true;
            break;
        }

        if (!hasVideo)
        {
            _isAudioOnly = true;
            return;
        }

        if (_isAudioOnly && !MediaFileHelper.IsAudioFile(_currentFilePath ?? string.Empty))
            _isAudioOnly = false;
    }

    private void UpdateVideoDimensions(VlcMedia media)
    {
        foreach (var track in media.Tracks)
        {
            if (track.TrackType != TrackType.Video)
                continue;

            var width = track.Data.Video.Width;
            var height = track.Data.Video.Height;
            if (width > 0 && height > 0)
                VideoFit.SetVideoSize(width, height);
            break;
        }
    }

    private void ShowPreviewFrameIfNeeded()
    {
        if (!_pendingPreviewFrame || _mediaPlayer is null)
            return;

        try
        {
            _mediaPlayer.Time = 0;
            _mediaPlayer.NextFrame();
            _pendingPreviewFrame = false;
        }
        catch
        {
            // retry when vout/parsed fires again
        }
    }

    public void PlayPause()
    {
        if (_mediaPlayer is null)
            return;

        if (_mediaPlayer.IsPlaying)
            _mediaPlayer.Pause();
        else if (_currentMedia is not null)
            _mediaPlayer.Play();
    }

    public void SeekTo(long timeMs)
    {
        if (_mediaPlayer is null || _currentMedia is null)
            return;

        _mediaPlayer.Time = Math.Clamp(timeMs, 0, Math.Max(_mediaPlayer.Length, 0));
    }

    public void SeekRelative(long deltaMs) => SeekTo(TimeMs + deltaMs);

    public void SetVolume(int volume)
    {
        if (_mediaPlayer is not null)
            _mediaPlayer.Volume = Math.Clamp(volume, 0, 100);
    }

    public void SetMute(bool mute)
    {
        if (_mediaPlayer is not null)
            _mediaPlayer.Mute = mute;
    }

    public void SetRate(float rate)
    {
        if (_mediaPlayer is null)
            return;

        _mediaPlayer.SetRate(Math.Clamp(rate, 0.25f, 2f));
        StateChanged?.Invoke();
    }

    public void LoadSubtitle(string subtitlePath)
    {
        _subtitlePath = subtitlePath;
        if (_mediaPlayer is not null)
            ApplySubtitle(subtitlePath);
    }

    public void ToggleSubtitles(bool enabled)
    {
        if (_mediaPlayer is null)
            return;

        if (enabled && !string.IsNullOrEmpty(_subtitlePath))
            ApplySubtitle(_subtitlePath);
        else
            _mediaPlayer.SetSpu(-1);
    }

    private void ApplySubtitle(string subtitlePath)
    {
        _mediaPlayer!.AddSlave(MediaSlaveType.Subtitle, subtitlePath, true);
        _mediaPlayer.SetSpu(0);
    }

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        var now = Environment.TickCount64;
        if (now - _lastTimeChangedMs < 200)
            return;

        _lastTimeChangedMs = now;
        InvokeOnUi(() => TimeChanged?.Invoke());
    }

    private static void InvokeOnUi(Action action)
    {
        if (App.DispatcherQueue?.HasThreadAccess == true)
        {
            action();
            return;
        }

        if (App.DispatcherQueue?.TryEnqueue(() => action()) != true)
            action();
    }

    private void OnEndReached(object? sender, EventArgs e)
        => InvokeOnUi(() => PlaybackEnded?.Invoke());

    private void OnEncounteredError(object? sender, EventArgs e)
        => InvokeOnUi(() =>
            PlaybackError?.Invoke("Could not play the file. It may be corrupted or the format is not supported."));

    private void StopInternal()
    {
        if (_mediaPlayer is null)
            return;

        if (_mediaPlayer.IsPlaying)
            _mediaPlayer.Stop();

        if (_currentMedia is not null)
        {
            _currentMedia.ParsedChanged -= OnMediaParsedChanged;
            _currentMedia.Dispose();
            _currentMedia = null;
        }

        _isAudioOnly = false;
        _currentFilePath = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_mediaPlayer is not null)
        {
            _mediaPlayer.TimeChanged -= _timeChangedHandler;
            _mediaPlayer.EndReached -= _endReachedHandler;
            _mediaPlayer.EncounteredError -= _errorHandler;
            _mediaPlayer.Playing -= _playingHandler;
            _mediaPlayer.Paused -= _pausedHandler;
            _mediaPlayer.Stopped -= _stoppedHandler;
            _mediaPlayer.Vout -= _voutHandler;
        }

        StopInternal();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _mediaPlayer = null;
        _libVlc = null;
        VideoZoom = null;
        VideoFit.Attach(null);
    }
}
