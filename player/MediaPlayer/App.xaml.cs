using System.Diagnostics;
using System.Text;
using MediaPlayer.Helpers;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

namespace MediaPlayer;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static InputSystemCursor HandCursor { get; } = InputSystemCursor.Create(InputSystemCursorShape.Hand);

    public static nint WindowHandle =>
        WinRT.Interop.WindowNative.GetWindowHandle(Window);

    static App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogCrash(ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash(args.Exception);
            args.SetObserved();
        };
    }

    public App()
    {
        LibVlcBootstrap.EnsureInitialized();
        ShellIntegrationService.EnsureRegistered();
        Helpers.FilePickerService.EnsureInitialized();
        UnhandledException += OnUnhandledException;
        InitializeComponent();
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);

        Debug.WriteLine(e.Exception);
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MediaUtilities", "player", "crash.log");
            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"{DateTime.Now:O}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}", Encoding.UTF8);
        }
        catch
        {
            // ignored
        }
    }

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        LaunchActivationService.RegisterCurrentInstance();
        if (!await LaunchActivationService.TryBecomeMainInstanceAsync())
            return;

        DispatcherQueue = DispatcherQueue.GetForCurrentThread();
        SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue));

        LaunchActivationService.FilesActivated += OnFilesActivated;

        Window = new MainWindow();
        Window.Activate();
    }

    private void OnFilesActivated(IReadOnlyList<string> files)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (Window is MainWindow mainWindow)
                mainWindow.OpenFiles(files);
        });
    }
}
