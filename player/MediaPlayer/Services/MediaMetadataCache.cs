using System.Collections.Concurrent;

namespace MediaPlayer.Services;

internal static class MediaMetadataCache
{
    private readonly record struct DurationEntry(long LastWriteTicks, TimeSpan Duration);

    private static readonly ConcurrentDictionary<string, DurationEntry> Durations =
        new(StringComparer.OrdinalIgnoreCase);

    public static bool TryGetDuration(string filePath, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (!Durations.TryGetValue(filePath, out var entry))
            return false;

        if (!TryGetWriteTicks(filePath, out var ticks) || ticks != entry.LastWriteTicks)
        {
            Durations.TryRemove(filePath, out _);
            return false;
        }

        duration = entry.Duration;
        return true;
    }

    public static void SetDuration(string filePath, TimeSpan duration)
    {
        if (!TryGetWriteTicks(filePath, out var ticks))
            return;

        Durations[filePath] = new DurationEntry(ticks, duration);
    }

    private static bool TryGetWriteTicks(string filePath, out long ticks)
    {
        ticks = 0;
        try
        {
            if (!File.Exists(filePath))
                return false;

            ticks = File.GetLastWriteTimeUtc(filePath).Ticks;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
