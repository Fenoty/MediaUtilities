using System.Reflection;

namespace MediaPlayer.Helpers;

public static class AppVersion
{
    public const string ProductName = "FPlayer";

    public static string Version { get; } = ReadVersion();

    public static string DefaultWindowTitle { get; } = $"{ProductName} {Version}";

    private static string ReadVersion()
    {
        var assembly = typeof(AppVersion).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+')[0];

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
