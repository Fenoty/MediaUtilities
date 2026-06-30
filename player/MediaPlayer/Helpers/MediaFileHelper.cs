namespace MediaPlayer.Helpers;

public static class MediaFileHelper
{
    public static readonly string[] VideoExtensions =
    [
        ".mp4", ".mkv", ".avi", ".webm", ".mov", ".flv", ".wmv",
        ".mpeg", ".mpg", ".m4v", ".ts", ".m2ts", ".mts", ".3gp", ".ogv",
        ".asf", ".wm", ".rmvb", ".rm", ".divx", ".f4v", ".vob", ".mxf", ".nut"
    ];

    public static readonly string[] AudioExtensions =
    [
        ".mp3", ".flac", ".wav", ".ogg", ".oga", ".opus", ".m4a",
        ".aac", ".wma", ".aiff", ".aif", ".alac", ".ape", ".wv",
        ".mka", ".mp2", ".ac3", ".dts", ".amr", ".mid", ".midi"
    ];

    public static readonly string[] ImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff",
        ".heic", ".heif", ".svg", ".ico", ".jfif", ".avif"
    ];

    private static readonly HashSet<string> KnownMediaExtensions =
        VideoExtensions.Concat(AudioExtensions).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ImageExtensionSet =
        ImageExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && ImageExtensionSet.Contains(ext);
    }

    public static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) &&
               VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsAudioFile(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) &&
               AudioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsKnownMediaExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && KnownMediaExtensions.Contains(ext);
    }

    public static bool IsMediaFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (IsImageFile(path))
            return false;

        if (IsKnownMediaExtension(path))
            return true;

        return File.Exists(path) && !string.IsNullOrEmpty(Path.GetExtension(path));
    }
}
