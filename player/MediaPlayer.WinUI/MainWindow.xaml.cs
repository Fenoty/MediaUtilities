using MediaPlayer.Helpers;
using MediaPlayer.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MediaPlayer;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private MainPage? _mainPage;
    private bool _wasPlaylistVisible;
    private WindowAspectRatioLock? _aspectRatioLock;
    private AppWindowPresenterKind _previousPresenter = AppWindowPresenterKind.Default;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppTitleBar.Title = ViewModel.WindowTitle;
        AppTitleBar.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle))
                AppTitleBar.Title = ViewModel.WindowTitle;
        };

        ViewModel.RequestToggleFullscreen += EnterFullscreen;
        ViewModel.RequestExitFullscreen += ExitFullscreen;
        ViewModel.ShowError += msg =>
        {
            _ = new ContentDialog
            {
                Title = "Ошибка",
                Content = msg,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
        };

        ViewModel.ApplyWindowSettings(this);
        Helpers.WindowSizeConstraints.Attach(this);

        var page = new MainPage();
        page.Initialize(ViewModel);
        PlayerHost.Content = page;
        _mainPage = page;
        page.EnableDragDrop();

        _aspectRatioLock = new WindowAspectRatioLock(this, page, ViewModel, AppTitleBar);
        _aspectRatioLock.Attach();

        if (ViewModel.LockWindowToVideoAspect)
            _aspectRatioLock.SnapToVideoAspect(preserveWidth: true);

        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        _mainPage?.SetBackgroundAnimationPaused(
            args.WindowActivationState == WindowActivationState.Deactivated);

        if (args.WindowActivationState == WindowActivationState.Deactivated)
            return;

        Content.KeyDown -= Content_KeyDown;
        Content.KeyDown += Content_KeyDown;
    }

    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && ViewModel.IsFullscreen)
        {
            ExitFullscreen();
            e.Handled = true;
            return;
        }

        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        _mainPage?.HandleKey(e.Key, ctrl);

        if (e.Key is VirtualKey.Space or VirtualKey.Left or VirtualKey.Right or VirtualKey.Up or VirtualKey.Down
            or VirtualKey.F or VirtualKey.M or VirtualKey.N or VirtualKey.P)
        {
            e.Handled = true;
        }
    }

    private void EnterFullscreen()
    {
        if (ViewModel.IsFullscreen || AppWindow is null)
            return;

        _wasPlaylistVisible = ViewModel.IsPlaylistVisible;
        ViewModel.IsFullscreen = true;
        ViewModel.IsPlaylistVisible = false;

        _previousPresenter = AppWindow.Presenter.Kind;
        if (AppWindow.Presenter is OverlappedPresenter overlapped)
            overlapped.SetBorderAndTitleBar(false, false);

        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
    }

    private void ExitFullscreen()
    {
        if (!ViewModel.IsFullscreen || AppWindow is null)
            return;

        ViewModel.IsFullscreen = false;
        ViewModel.IsPlaylistVisible = _wasPlaylistVisible;

        AppWindow.SetPresenter(_previousPresenter);
        if (AppWindow.Presenter is OverlappedPresenter overlapped)
            overlapped.SetBorderAndTitleBar(true, true);

        _aspectRatioLock?.SnapToVideoAspect(preserveWidth: true);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        ViewModel.SaveSettings(this);
        ViewModel.Dispose();
    }
}
