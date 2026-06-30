using System.Globalization;

namespace MediaPlayer.Helpers;

public static class TimeFormat
{
    public static string Format(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return time.ToString(@"h\:mm\:ss", CultureInfo.CurrentCulture);

        return time.ToString(@"m\:ss", CultureInfo.CurrentCulture);
    }
}
