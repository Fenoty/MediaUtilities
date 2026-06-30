using LibVLCSharp.Shared;

namespace MediaPlayer.Helpers;

public static class LibVlcBootstrap
{
    private static readonly object Gate = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (Gate)
        {
            if (_initialized)
                return;

            var libvlcPath = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
            if (Directory.Exists(libvlcPath))
                Core.Initialize(libvlcPath);
            else
                Core.Initialize();

            _initialized = true;
        }
    }
}
