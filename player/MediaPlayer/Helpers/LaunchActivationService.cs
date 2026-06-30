using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace MediaPlayer.Helpers;

public static class LaunchActivationService
{
    private static readonly List<string> PendingFiles = [];
    private static bool _subscribed;

    public static event Action<IReadOnlyList<string>>? FilesActivated;

    public static void RegisterCurrentInstance()
    {
        var instance = AppInstance.FindOrRegisterForKey(AppVersion.ProductName);
        if (!_subscribed)
        {
            instance.Activated += OnAppActivated;
            _subscribed = true;
        }

        CollectFiles(instance.GetActivatedEventArgs());
    }

    public static async Task<bool> TryBecomeMainInstanceAsync()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey(AppVersion.ProductName);
        if (mainInstance.IsCurrent)
            return true;

        var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        await mainInstance.RedirectActivationToAsync(activatedEventArgs);
        Environment.Exit(0);
        return false;
    }

    public static IReadOnlyList<string> ConsumePendingFiles()
    {
        lock (PendingFiles)
        {
            if (PendingFiles.Count == 0)
                return [];

            var files = PendingFiles.ToList();
            PendingFiles.Clear();
            return files;
        }
    }

    private static void OnAppActivated(object? sender, AppActivationArguments e)
    {
        var files = ExtractFiles(e);
        if (files.Count == 0)
            return;

        FilesActivated?.Invoke(files);
    }

    private static void CollectFiles(AppActivationArguments? args)
    {
        if (args is not null)
            QueueFiles(ExtractFiles(args));

        QueueFiles(GetCommandLineFiles());
    }

    private static void QueueFiles(IEnumerable<string> files)
    {
        lock (PendingFiles)
        {
            foreach (var file in files)
            {
                if (PendingFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
                    continue;

                PendingFiles.Add(file);
            }
        }
    }

    private static List<string> ExtractFiles(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.File && args.Kind != ExtendedActivationKind.Launch)
            return [];

        if (args.Data is IFileActivatedEventArgs fileArgs)
        {
            return fileArgs.Files
                .Select(file => file.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .ToList();
        }

        return GetCommandLineFiles().ToList();
    }

    private static IEnumerable<string> GetCommandLineFiles()
    {
        return Environment.GetCommandLineArgs()
            .Skip(1)
            .Select(NormalizeArgument)
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string NormalizeArgument(string argument)
    {
        var trimmed = argument.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1];

        return trimmed;
    }
}
