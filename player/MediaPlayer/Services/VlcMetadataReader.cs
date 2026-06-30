using System.Diagnostics;
using LibVLCSharp.Shared;
using MediaPlayer.Helpers;
using VlcMedia = LibVLCSharp.Shared.Media;

namespace MediaPlayer.Services;

internal static class VlcMetadataReader
{
    private static readonly object Gate = new();

    public static TimeSpan GetDuration(string filePath)
    {
        lock (Gate)
        {
            try
            {
                using var media = new VlcMedia(VlcAuxiliaryService.Instance, filePath, FromType.FromPath);
                var parsed = new ManualResetEventSlim(false);

                void OnParsed(object? _, MediaParsedChangedEventArgs __)
                {
                    if (media.IsParsed)
                        parsed.Set();
                }

                media.ParsedChanged += OnParsed;
                try
                {
                    media.Parse(MediaParseOptions.ParseLocal);
                    parsed.Wait(TimeSpan.FromSeconds(5));
                }
                finally
                {
                    media.ParsedChanged -= OnParsed;
                }

                var durationMs = media.Duration;
                return durationMs > 0
                    ? TimeSpan.FromMilliseconds(durationMs)
                    : TimeSpan.Zero;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VLC duration failed for {filePath}: {ex.Message}");
                return TimeSpan.Zero;
            }
        }
    }
}
