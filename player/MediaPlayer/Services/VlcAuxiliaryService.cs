using LibVLCSharp.Shared;
using MediaPlayer.Helpers;

namespace MediaPlayer.Services;

public static class VlcAuxiliaryService
{
    private static readonly object Gate = new();
    private static LibVLC? _libVlc;

    public static LibVLC Instance
    {
        get
        {
            lock (Gate)
            {
                LibVlcBootstrap.EnsureInitialized();
                _libVlc ??= new LibVLC("--intf=dummy", "--no-audio", "--quiet");
                return _libVlc;
            }
        }
    }

    public static void Dispose()
    {
        lock (Gate)
        {
            _libVlc?.Dispose();
            _libVlc = null;
        }
    }
}
