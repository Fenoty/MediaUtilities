using System.Collections.Concurrent;
using System.Diagnostics;
using MediaPlayer.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace MediaPlayer.Services;

public sealed class ThumbnailService
{
    private const int ThumbnailSize = 320;
    private static readonly SemaphoreSlim LoadGate = new(2, 2);
    private static readonly ConcurrentDictionary<string, Task<TimeSpan>> DurationInFlight =
        new(StringComparer.OrdinalIgnoreCase);

    public ThumbnailService()
    {
        Directory.CreateDirectory(GetCacheDirectory());
    }

    public Task<TimeSpan> LoadDurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (MediaMetadataCache.TryGetDuration(filePath, out var cached))
            return Task.FromResult(cached);

        return DurationInFlight.GetOrAdd(filePath, path => LoadDurationCoreAsync(path, cancellationToken));
    }

    private async Task<TimeSpan> LoadDurationCoreAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            await LoadGate.WaitAsync(cancellationToken);
            if (MediaMetadataCache.TryGetDuration(filePath, out var cached))
                return cached;

            var duration = await Task.Run(() => ShellDurationReader.TryGetDuration(filePath), cancellationToken);
            MediaMetadataCache.SetDuration(filePath, duration);
            return duration;
        }
        finally
        {
            LoadGate.Release();
            DurationInFlight.TryRemove(filePath, out _);
        }
    }

    public static Task<BitmapImage?> LoadFileThumbnailAsync(string filePath)
    {
        return UiDispatcher.InvokeAsync(async () =>
        {
            try
            {
                var cached = GetCachedPath(filePath);
                if (File.Exists(cached))
                    return await LoadBitmapFromPathAsync(cached);

                var file = await StorageFile.GetFileFromPathAsync(filePath);
                var modes = MediaFileHelper.IsAudioFile(filePath)
                    ? new[] { ThumbnailMode.MusicView, ThumbnailMode.SingleItem }
                    : new[] { ThumbnailMode.VideosView, ThumbnailMode.SingleItem, ThumbnailMode.PicturesView };

                foreach (var mode in modes)
                {
                    using var thumbnail = await file.GetThumbnailAsync(
                        mode,
                        ThumbnailSize,
                        ThumbnailOptions.ResizeThumbnail | ThumbnailOptions.UseCurrentScale);

                    if (thumbnail.Type == ThumbnailType.Icon || thumbnail.OriginalWidth == 0)
                        continue;

                    var bitmap = new BitmapImage();
                    using (IRandomAccessStream stream = thumbnail)
                        await bitmap.SetSourceAsync(stream);

                    _ = SaveThumbnailToCacheAsync(filePath, cached);
                    return bitmap;
                }

                if (ShellThumbnailProvider.TrySaveThumbnail(filePath, cached, ThumbnailSize))
                    return await LoadBitmapFromPathAsync(cached);

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Thumbnail load failed for {filePath}: {ex.Message}");
                return null;
            }
        });
    }

    public static Task<BitmapImage?> LoadBitmapAsync(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return Task.FromResult<BitmapImage?>(null);

        return UiDispatcher.InvokeAsync(() => LoadBitmapFromPathAsync(path));
    }

    private static async Task<BitmapImage?> LoadBitmapFromPathAsync(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var image = new BitmapImage
            {
                UriSource = new Uri($"file:///{fullPath.Replace('\\', '/')}"),
                DecodePixelWidth = ThumbnailSize
            };
            return image;
        }
        catch
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);
                return image;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static string GetCacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MediaUtilities",
            "player",
            "thumbs");

    internal static string GetCachedPath(string filePath)
    {
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(filePath.ToLowerInvariant())));
        return Path.Combine(GetCacheDirectory(), $"{hash}.png");
    }

    private static async Task SaveThumbnailToCacheAsync(string filePath, string cachedPath)
    {
        try
        {
            if (File.Exists(cachedPath))
                return;

            var file = await StorageFile.GetFileFromPathAsync(filePath);
            using var thumbnail = await file.GetThumbnailAsync(
                ThumbnailMode.VideosView,
                ThumbnailSize,
                ThumbnailOptions.ResizeThumbnail | ThumbnailOptions.UseCurrentScale);

            if (thumbnail.Type == ThumbnailType.Icon || thumbnail.OriginalWidth == 0)
                return;

            Directory.CreateDirectory(GetCacheDirectory());
            using var fileStream = File.Open(cachedPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using IRandomAccessStream stream = thumbnail;
            await stream.AsStreamForRead().CopyToAsync(fileStream);
        }
        catch
        {
            // cache is optional
        }
    }
}

file static class ShellDurationReader
{
    public static TimeSpan TryGetDuration(string path)
    {
        try
        {
            var task = GetDurationAsync(path);
            return task.GetAwaiter().GetResult();
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    private static async Task<TimeSpan> GetDurationAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        if (MediaFileHelper.IsAudioFile(path))
        {
            var music = await file.Properties.GetMusicPropertiesAsync();
            return music.Duration;
        }

        var props = await file.Properties.GetVideoPropertiesAsync();
        return props.Duration;
    }
}

file static class ShellThumbnailProvider
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Cx;
        public int Cy;
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem;

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("bcc18b79-ba16-442f-80c4-8a59c30c963b")]
    [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [System.Runtime.InteropServices.PreserveSig]
        int GetImage(NativeSize size, int flags, out IntPtr phbm);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IShellItemImageFactory ppv);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int SiigbfThumbnailOnly = 0x8;
    private const int SiigbfResizeToFit = 0x2;
    private const int SiigbfBigGons = 0x20;

    public static bool TrySaveThumbnail(string path, string outputPath, int size)
    {
        foreach (var flags in new[] { SiigbfThumbnailOnly | SiigbfResizeToFit, SiigbfBigGons | SiigbfResizeToFit })
        {
            if (TrySaveThumbnailCore(path, outputPath, size, flags))
                return true;
        }

        return false;
    }

    private static bool TrySaveThumbnailCore(string path, string outputPath, int size, int flags)
    {
        IntPtr hBitmap = IntPtr.Zero;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var guid = typeof(IShellItemImageFactory).GUID;
            var hr = SHCreateItemFromParsingName(fullPath, IntPtr.Zero, ref guid, out var factory);
            if (hr != 0 || factory == null)
                return false;

            var nativeSize = new NativeSize { Cx = size, Cy = size };
            if (factory.GetImage(nativeSize, flags, out hBitmap) != 0 || hBitmap == IntPtr.Zero)
                return false;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            using var bitmap = System.Drawing.Image.FromHbitmap(hBitmap);
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            return File.Exists(outputPath);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
        }
    }
}
