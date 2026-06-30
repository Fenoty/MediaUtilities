using System.Diagnostics;
using LibVLCSharp.Shared;
using MediaPlayer.Helpers;
using VlcMedia = LibVLCSharp.Shared.Media;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace MediaPlayer.Services;

internal static class VlcThumbnailExtractor
{
    private static readonly object Gate = new();

    public static bool TrySaveThumbnail(string filePath, string outputPath, int size, string? skipIfPlayingPath = null)
    {
        if (MediaFileHelper.IsAudioFile(filePath))
            return false;

        if (skipIfPlayingPath is not null &&
            string.Equals(Path.GetFullPath(filePath), Path.GetFullPath(skipIfPlayingPath), StringComparison.OrdinalIgnoreCase))
            return false;

        lock (Gate)
        {
            try
            {
                var libVlc = VlcAuxiliaryService.Instance;
                using var media = new VlcMedia(libVlc, filePath, FromType.FromPath);
                media.AddOption(":no-audio");

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

                if (media.Duration <= 0)
                    return false;

                using var player = new VlcMediaPlayer(libVlc);
                player.Media = media;
                player.Play();

                var seekMs = (long)Math.Clamp(media.Duration * 0.1, 1000, 8000);
                var ready = new ManualResetEventSlim(false);
                void OnTimeChanged(object? _, MediaPlayerTimeChangedEventArgs __)
                {
                    if (player.Time >= seekMs - 500)
                        ready.Set();
                }

                player.TimeChanged += OnTimeChanged;
                try
                {
                    player.Time = seekMs;
                    ready.Wait(TimeSpan.FromSeconds(4));
                }
                finally
                {
                    player.TimeChanged -= OnTimeChanged;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                player.TakeSnapshot(0, outputPath, (uint)size, (uint)size);
                player.Stop();

                return File.Exists(outputPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VLC thumbnail failed for {filePath}: {ex.Message}");
                return false;
            }
        }
    }
}
