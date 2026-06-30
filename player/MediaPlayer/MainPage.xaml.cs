using System.ComponentModel;
using LibVLCSharp.Platforms.Windows;
using MediaPlayer.Controls;
using MediaPlayer.Helpers;
using MediaPlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace MediaPlayer;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; private set; } = null!;

    internal Border MenuBarChrome => MenuBarBorder;
    internal Border ControlBarChrome => ControlBarBorder;
    internal Border PlaylistChrome => PlaylistPanel;

    private string? _pendingPlaybackPath;
    private bool _pendingAutoPlay;
    private bool _videoViewInitialized;
    private EventHandler<object>? _videoSurfaceLayoutHandler;

    private InitializedEventArgs? _pendingVideoViewInit;

    private bool _progressDragging;

    private readonly List<RadioMenuFlyoutItem> _videoFitMenuItems = [];
    private readonly MenuFlyout _videoFitToolbarFlyout = new();

    public MainPage()
    {
        InitializeComponent();
        VideoView.Initialized += OnVideoViewInitialized;
        Loaded += (_, _) => UpdateAdaptiveLayout(RootGrid.ActualWidth);
    }

    public void Initialize(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        Bindings.Update();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.VideoFitChanged += OnVideoFitChanged;
        ViewModel.RequestStartPlayback += OnRequestStartPlayback;
        BuildVideoFitMenu();
        ApplyMediaSurfaceState();

        if (_pendingVideoViewInit is not null)
            ApplyVideoViewInitialized(_pendingVideoViewInit);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.HasActiveMedia) or nameof(MainViewModel.IsAudioMode))
            ApplyMediaSurfaceState();
    }

    private void OnVideoViewInitialized(object? sender, InitializedEventArgs e)
    {
        if (ViewModel is null)
        {
            _pendingVideoViewInit = e;
            return;
        }

        ApplyVideoViewInitialized(e);
    }

    private void ApplyVideoViewInitialized(InitializedEventArgs e)
    {
        ViewModel.InitializePlayback(e);
        if (!ViewModel.IsPlaybackReady)
            return;

        VideoView.MediaPlayer = ViewModel.LibVlcMediaPlayer;
        _videoViewInitialized = true;
        TryCompletePendingPlayback();
    }

    private void ApplyMediaSurfaceState()
    {
        if (ViewModel is null)
            return;

        var active = ViewModel.HasActiveMedia;
        EmptyStateOverlay.Visibility = active ? Visibility.Collapsed : Visibility.Visible;
        VideoClickLayer.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        AudioPlaybackPanel.Visibility = ViewModel.IsAudioMode && active
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!active || ViewModel.IsAudioMode)
            ViewModel.VideoZoom?.Reset();

        if (!active || ViewModel.VideoZoom?.IsZoomed == true || ViewModel.IsAudioMode)
            ApplyVideoFit();
    }

    private void OnRequestStartPlayback(string path, bool autoPlay)
    {
        _pendingPlaybackPath = path;
        _pendingAutoPlay = autoPlay;
        ApplyMediaSurfaceState();
        TryCompletePendingPlayback();
    }

    private void TryCompletePendingPlayback()
    {
        void Attempt()
        {
            if (_pendingPlaybackPath is null)
                return;

            if (!_videoViewInitialized || !ViewModel.IsPlaybackReady)
            {
                if (_videoSurfaceLayoutHandler is not null)
                    return;

                _videoSurfaceLayoutHandler = (_, _) =>
                {
                    if (!_videoViewInitialized || !ViewModel.IsPlaybackReady)
                        return;

                    if (!ViewModel.IsAudioMode && !IsVideoSurfaceReady())
                        return;

                    CompletePendingPlayback();
                };

                VideoView.LayoutUpdated += _videoSurfaceLayoutHandler;
                return;
            }

            if (!ViewModel.IsAudioMode && !IsVideoSurfaceReady())
            {
                if (_videoSurfaceLayoutHandler is not null)
                    return;

                _videoSurfaceLayoutHandler = (_, _) =>
                {
                    if (!_videoViewInitialized || !ViewModel.IsPlaybackReady || !IsVideoSurfaceReady())
                        return;

                    CompletePendingPlayback();
                };

                VideoView.LayoutUpdated += _videoSurfaceLayoutHandler;
                return;
            }

            CompletePendingPlayback();
        }

        DispatcherQueue.TryEnqueue(Attempt);
    }

    private bool IsVideoSurfaceReady()
        => ViewModel.IsAudioMode || (VideoView.ActualWidth > 0 && VideoView.ActualHeight > 0);

    private void CompletePendingPlayback()
    {
        if (_videoSurfaceLayoutHandler is not null)
        {
            VideoView.LayoutUpdated -= _videoSurfaceLayoutHandler;
            _videoSurfaceLayoutHandler = null;
        }

        var path = _pendingPlaybackPath;
        var autoPlay = _pendingAutoPlay;
        _pendingPlaybackPath = null;

        if (path is null)
            return;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            ViewModel.LoadMedia(path, autoPlay);
            ApplyVideoFit();
        });
    }

    private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            await ViewModel.PromptOpenFileAsync());
    }

    private void AddToPlaylistMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
            await ViewModel.PromptAddToPlaylistAsync());
    }

    private async void AddToPlaylistButton_Click(object sender, RoutedEventArgs e)
        => await ViewModel.PromptAddToPlaylistAsync();

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        => Application.Current.Exit();

    private void EmptyState_Click(object sender, RoutedEventArgs e)
        => ViewModel.TogglePlayPauseFromVideo();

    private void EmptyStateRoot_PointerEntered(object sender, PointerRoutedEventArgs e)
        => EmptyStateGlow.Opacity = 1;

    private void EmptyStateRoot_PointerExited(object sender, PointerRoutedEventArgs e)
        => EmptyStateGlow.Opacity = 0;

    private void VideoArea_Click(object sender, RoutedEventArgs e)
        => ViewModel.TogglePlayPauseFromVideo();

    private void VideoArea_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!ViewModel.HasActiveMedia || ViewModel.IsAudioMode || VideoHost.ActualWidth <= 0 || VideoHost.ActualHeight <= 0)
            return;

        var delta = e.GetCurrentPoint(VideoHost).Properties.MouseWheelDelta;
        if (delta == 0)
            return;

        if (ViewModel.VideoZoom?.AdjustAt(
                VideoHost.ActualWidth,
                VideoHost.ActualHeight,
                e.GetCurrentPoint(VideoHost).Position.X,
                e.GetCurrentPoint(VideoHost).Position.Y,
                ViewModel.SelectedVideoFit?.Mode ?? VideoFitMode.Fit,
                ViewModel.VideoWidth,
                ViewModel.VideoHeight,
                delta) == true)
        {
            ApplyVideoFit();
            e.Handled = true;
        }
    }

    private void ProgressSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _progressDragging = true;
        ViewModel.BeginSeek();
        if (sender is Slider slider)
            ViewModel.PreviewSeek(slider.Value);
    }

    private void ProgressSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _progressDragging = false;
        if (sender is Slider slider)
            ViewModel.CommitSeek(slider.Value);
    }

    private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_progressDragging)
            ViewModel.PreviewSeek(e.NewValue);
    }

    public void EnableDragDrop()
    {
        AllowDrop = true;
        DragOver += Page_DragOver;
        Drop += Page_Drop;
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        e.Handled = true;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.OfType<Windows.Storage.StorageFile>()
            .Select(f => f.Path)
            .Where(p => File.Exists(p) && MediaFileHelper.IsMediaFile(p))
            .ToArray();

        if (paths.Length > 0)
            ViewModel.OpenFiles(paths);
    }

    public void HandleKey(VirtualKey key, bool ctrl)
        => ViewModel.HandleKey(key, ctrl);

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        => UpdateAdaptiveLayout(e.NewSize.Width);

    private void UpdateAdaptiveLayout(double width)
    {
        if (width <= 0)
            return;

        VisualStateManager.GoToState(
            this,
            AdaptiveLayout.GetWindowWidthVisualState(width),
            true);

        EmptyStatePanel.MaxWidth = Math.Max(260, width - 48);

        var playlistWidth = AdaptiveLayout.GetPlaylistBaseWidth(width);
        PlaylistPanel.Width = playlistWidth;
        PlaylistPanel.MaxWidth = AdaptiveLayout.BasePlaylistWidth;
    }

    private void VideoHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVideoHostClip();
        ApplyVideoFit();
    }

    private void UpdateVideoHostClip()
    {
        if (VideoHost.ActualWidth <= 0 || VideoHost.ActualHeight <= 0)
            return;

        VideoHost.Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, VideoHost.ActualWidth, VideoHost.ActualHeight)
        };
    }

    private void OnVideoFitChanged()
    {
        ApplyVideoFit();
        RefreshVideoFitMenuSelection();
    }

    private void ApplyVideoFit()
    {
        if (ViewModel is null || VideoHost.ActualWidth <= 0 || VideoHost.ActualHeight <= 0)
            return;

        ViewModel.ApplyVideoFit(VideoHost.ActualWidth, VideoHost.ActualHeight);
    }

    private void BuildVideoFitMenu()
    {
        foreach (var item in _videoFitMenuItems)
            item.Click -= OnVideoFitMenuItemClick;

        _videoFitMenuItems.Clear();
        VideoFitSubMenu.Items.Clear();
        VideoFitOverflowSubMenu.Items.Clear();
        _videoFitToolbarFlyout.Items.Clear();

        foreach (var option in MainViewModel.VideoFitOptions)
        {
            VideoFitSubMenu.Items.Add(CreateVideoFitMenuItem(option));
            VideoFitOverflowSubMenu.Items.Add(CreateVideoFitMenuItem(option));
            _videoFitToolbarFlyout.Items.Add(CreateVideoFitMenuItem(option));
        }

        BtnVideoFit.Flyout = _videoFitToolbarFlyout;
        RefreshVideoFitMenuSelection();
    }

    private RadioMenuFlyoutItem CreateVideoFitMenuItem(VideoFitOption option)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = option.Label,
            Tag = option,
            GroupName = "VideoFitGroup",
            IsChecked = ViewModel.SelectedVideoFit?.Mode == option.Mode
        };
        item.Click += OnVideoFitMenuItemClick;
        _videoFitMenuItems.Add(item);
        return item;
    }

    private void OnVideoFitMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: VideoFitOption option })
            ViewModel.SelectedVideoFit = option;
    }

    private void RefreshVideoFitMenuSelection()
    {
        if (ViewModel is null)
            return;

        foreach (var item in _videoFitMenuItems)
        {
            if (item.Tag is VideoFitOption option)
                item.IsChecked = ViewModel.SelectedVideoFit?.Mode == option.Mode;
        }
    }

    private void PlaylistRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Index >= 0)
            ViewModel.RequestPlaylistItemMetadata(args.Index);
    }

    public void SetBackgroundAnimationPaused(bool paused)
        => RootGrid.SetAnimationPaused(paused);
}
