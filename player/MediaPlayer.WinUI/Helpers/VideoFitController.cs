using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace MediaPlayer.Helpers;

public sealed class VideoFitController
{
    private VlcMediaPlayer? _player;
    private VideoFitMode _mode = VideoFitMode.Fit;
    private uint _videoWidth;
    private uint _videoHeight;

    public VideoFitMode Mode
    {
        get => _mode;
        set => _mode = value;
    }

    public uint VideoWidth => _videoWidth;
    public uint VideoHeight => _videoHeight;

    public void Attach(VlcMediaPlayer? player) => _player = player;

    public void SetVideoSize(uint width, uint height)
    {
        if (width > 0 && height > 0)
        {
            _videoWidth = width;
            _videoHeight = height;
        }
    }

    public void Apply(double viewWidth, double viewHeight, double zoom = 1.0, double focalX = 0.5, double focalY = 0.5)
    {
        if (_player is null || viewWidth <= 1 || viewHeight <= 1)
            return;

        ApplyBase(viewWidth, viewHeight);

        zoom = Math.Clamp(zoom, VideoZoomController.MinZoom, VideoZoomController.MaxZoom);
        if (zoom <= VideoZoomController.MinZoom + 0.001 || !TryGetVideoSize(out var videoWidth, out var videoHeight))
        {
            _player.CropGeometry = null;
            return;
        }

        var cropWidth = videoWidth / zoom;
        var cropHeight = videoHeight / zoom;
        var cropX = Math.Clamp(focalX * videoWidth - cropWidth / 2, 0, videoWidth - cropWidth);
        var cropY = Math.Clamp(focalY * videoHeight - cropHeight / 2, 0, videoHeight - cropHeight);

        _player.CropGeometry =
            $"{(int)Math.Round(cropWidth)}x{(int)Math.Round(cropHeight)}+{(int)Math.Round(cropX)}+{(int)Math.Round(cropY)}";
    }

    private void ApplyBase(double viewWidth, double viewHeight)
    {
        if (_player is null)
            return;

        switch (_mode)
        {
            case VideoFitMode.Fit:
                _player.AspectRatio = null;
                _player.Scale = 0;
                break;

            case VideoFitMode.Stretch:
                _player.Scale = 0;
                _player.AspectRatio = $"{(int)viewWidth}:{(int)viewHeight}";
                break;

            case VideoFitMode.Fill:
                ApplyFill(viewWidth, viewHeight);
                break;
        }
    }

    private void ApplyFill(double viewWidth, double viewHeight)
    {
        if (_player is null)
            return;

        if (!TryGetVideoSize(out var videoWidth, out var videoHeight))
        {
            _player.Scale = 0;
            _player.AspectRatio = $"{(int)viewWidth}:{(int)viewHeight}";
            return;
        }

        var scale = Math.Max(viewWidth / videoWidth, viewHeight / videoHeight);
        _player.AspectRatio = null;
        _player.Scale = (float)Math.Max(0.01, scale);
    }

    private bool TryGetVideoSize(out double width, out double height)
    {
        width = _videoWidth;
        height = _videoHeight;

        if (width > 0 && height > 0)
            return true;

        if (_player is null)
            return false;

        uint w = 0, h = 0;
        if (_player.Size(0, ref w, ref h) && w > 0 && h > 0)
        {
            width = w;
            height = h;
            return true;
        }

        return false;
    }
}
