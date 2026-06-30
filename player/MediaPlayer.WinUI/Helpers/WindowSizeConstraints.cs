using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace MediaPlayer.Helpers;

public static class WindowSizeConstraints
{
    private static bool _isClamping;

    public static Func<bool>? IsAspectRatioLockActive { get; set; }

    public static void Attach(Window window)
    {
        if (window.AppWindow is null)
            return;

        ApplyPreferredMinimum(window);
        ClampSize(window);

        window.AppWindow.Changed += (_, args) =>
        {
            if (args.DidSizeChange)
                ClampSize(window);
        };

        window.Activated += (_, _) =>
        {
            ApplyPreferredMinimum(window);
            ClampSize(window);
        };
    }

    public static void ApplyPreferredMinimum(Window window)
    {
        if (window.AppWindow?.Presenter is OverlappedPresenter overlapped)
        {
            overlapped.PreferredMinimumWidth = AdaptiveLayout.MinWindowWidth;
            overlapped.PreferredMinimumHeight = AdaptiveLayout.MinWindowHeight;
        }
    }

    private static void ClampSize(Window window)
    {
        if (IsAspectRatioLockActive?.Invoke() == true)
            return;

        if (_isClamping || window.AppWindow is null)
            return;

        var appWindow = window.AppWindow;
        var size = appWindow.Size;
        var width = Math.Max(AdaptiveLayout.MinWindowWidth, size.Width);
        var height = Math.Max(AdaptiveLayout.MinWindowHeight, size.Height);

        if (width == size.Width && height == size.Height)
            return;

        _isClamping = true;
        try
        {
            appWindow.Resize(new SizeInt32(width, height));
        }
        finally
        {
            _isClamping = false;
        }
    }
}
