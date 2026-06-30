using MediaPlayer;
using MediaPlayer.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace MediaPlayer.Helpers;

public sealed class WindowAspectRatioLock
{
    private readonly Window _window;
    private readonly MainPage _page;
    private readonly MainViewModel _viewModel;
    private readonly TitleBar _titleBar;

    private bool _isApplying;
    private SizeInt32 _lastSize;

    public WindowAspectRatioLock(Window window, MainPage page, MainViewModel viewModel, TitleBar titleBar)
    {
        _window = window;
        _page = page;
        _viewModel = viewModel;
        _titleBar = titleBar;
    }

    public void Attach()
    {
        WindowSizeConstraints.IsAspectRatioLockActive = IsActive;

        if (_window.AppWindow is null)
            return;

        _window.AppWindow.Changed += OnAppWindowChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.VideoFitChanged += OnVideoLayoutChanged;
        _lastSize = _window.AppWindow.Size;
    }

    private bool IsActive()
        => _viewModel.LockWindowToVideoAspect && CanLock();

    private bool CanLock()
        => !_viewModel.IsFullscreen
           && _viewModel.IsVideoMode
           && _viewModel.VideoWidth > 0
           && _viewModel.VideoHeight > 0;

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.LockWindowToVideoAspect)
            or nameof(MainViewModel.IsPlaylistVisible)
            or nameof(MainViewModel.IsFullscreen)
            or nameof(MainViewModel.IsVideoMode))
        {
            if (_viewModel.LockWindowToVideoAspect && CanLock())
                SnapToVideoAspect(preserveWidth: true);

            UpdateLastSize();
        }
    }

    private void OnVideoLayoutChanged()
    {
        if (IsActive())
            SnapToVideoAspect(preserveWidth: true);
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (!args.DidSizeChange || _isApplying || !IsActive())
        {
            if (!args.DidSizeChange)
                return;

            _lastSize = sender.Size;
            return;
        }

        var size = sender.Size;
        if (_lastSize.Width <= 0 || _lastSize.Height <= 0)
        {
            SnapToVideoAspect(preserveWidth: true);
            return;
        }

        var aspect = GetVideoAspect();
        var (horizontalChrome, verticalChrome) = GetChromeInsets();

        var deltaWidth = size.Width - _lastSize.Width;
        var deltaHeight = size.Height - _lastSize.Height;

        int newWidth;
        int newHeight;

        if (Math.Abs(deltaWidth) >= Math.Abs(deltaHeight))
        {
            newWidth = Math.Max(AdaptiveLayout.MinWindowWidth, size.Width);
            var videoWidth = Math.Max(1, newWidth - horizontalChrome);
            var videoHeight = videoWidth / aspect;
            newHeight = (int)Math.Round(videoHeight + verticalChrome);
        }
        else
        {
            newHeight = Math.Max(AdaptiveLayout.MinWindowHeight, size.Height);
            var videoHeight = Math.Max(1, newHeight - verticalChrome);
            var videoWidth = videoHeight * aspect;
            newWidth = (int)Math.Round(videoWidth + horizontalChrome);
        }

        newWidth = Math.Max(AdaptiveLayout.MinWindowWidth, newWidth);
        newHeight = Math.Max(AdaptiveLayout.MinWindowHeight, newHeight);

        if (newWidth == size.Width && newHeight == size.Height)
        {
            _lastSize = size;
            return;
        }

        ApplySize(sender, newWidth, newHeight);
    }

    public void SnapToVideoAspect(bool preserveWidth = false)
    {
        if (_window.AppWindow is null || !CanLock() || !_viewModel.LockWindowToVideoAspect)
            return;

        _page.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window.AppWindow is null || !CanLock())
                return;

            var aspect = GetVideoAspect();
            var (horizontalChrome, verticalChrome) = GetChromeInsets();
            var current = _window.AppWindow.Size;

            int newWidth;
            int newHeight;

            if (preserveWidth && current.Width > 0)
            {
                newWidth = Math.Max(AdaptiveLayout.MinWindowWidth, current.Width);
                var videoWidth = Math.Max(1, newWidth - horizontalChrome);
                newHeight = (int)Math.Round(videoWidth / aspect + verticalChrome);
            }
            else if (current.Height > verticalChrome)
            {
                newHeight = Math.Max(AdaptiveLayout.MinWindowHeight, current.Height);
                var videoHeight = Math.Max(1, newHeight - verticalChrome);
                newWidth = (int)Math.Round(videoHeight * aspect + horizontalChrome);
            }
            else
            {
                newWidth = Math.Max(AdaptiveLayout.MinWindowWidth, current.Width);
                var videoWidth = Math.Max(1, newWidth - horizontalChrome);
                newHeight = (int)Math.Round(videoWidth / aspect + verticalChrome);
            }

            newWidth = Math.Max(AdaptiveLayout.MinWindowWidth, newWidth);
            newHeight = Math.Max(AdaptiveLayout.MinWindowHeight, newHeight);
            ApplySize(_window.AppWindow, newWidth, newHeight);
        });
    }

    private void ApplySize(AppWindow appWindow, int width, int height)
    {
        _isApplying = true;
        try
        {
            appWindow.Resize(new SizeInt32(width, height));
            _lastSize = new SizeInt32(width, height);
        }
        finally
        {
            _isApplying = false;
        }
    }

    private void UpdateLastSize()
    {
        if (_window.AppWindow is not null)
            _lastSize = _window.AppWindow.Size;
    }

    private double GetVideoAspect()
        => (double)_viewModel.VideoWidth / _viewModel.VideoHeight;

    private (int Horizontal, int Vertical) GetChromeInsets()
    {
        var horizontal = 0;
        if (_viewModel.IsPlaylistVisible && _page.PlaylistChrome.Visibility == Visibility.Visible)
            horizontal = (int)Math.Ceiling(_page.PlaylistChrome.ActualWidth);

        var vertical = (int)Math.Ceiling(_titleBar.ActualHeight);
        vertical += (int)Math.Ceiling(_page.MenuBarChrome.ActualHeight);
        vertical += (int)Math.Ceiling(_page.ControlBarChrome.ActualHeight);

        if (vertical <= 0)
            vertical = 152;

        return (horizontal, vertical);
    }
}
