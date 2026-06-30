using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MediaPlayer.Helpers;

public static class ShellIntegrationService
{
    private const string AppRegistryName = "FPlayer";
    private const string CapabilitiesKey = @"Software\FPlayer\Capabilities";
    private const string FileAssociationsKey = @"Software\FPlayer\Capabilities\FileAssociations";

    public static void EnsureRegistered()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            return;

        RegisterApplication(exePath);
        RegisterFileAssociations(exePath);
        RegisterDefaultAppCapabilities();
        NotifyAssociationChanged();
    }

    private static void RegisterApplication(string exePath)
    {
        var openCommand = $"\"{exePath}\" \"%1\"";
        var icon = $"\"{exePath}\",0";

        using var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{Path.GetFileName(exePath)}");
        appKey?.SetValue("FriendlyAppName", AppVersion.ProductName);
        appKey?.SetValue("ApplicationCompany", AppVersion.CompanyName);
        using var commandKey = appKey?.CreateSubKey(@"shell\open\command");
        commandKey?.SetValue(string.Empty, openCommand);

        using var appClassKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{AppRegistryName}");
        appClassKey?.SetValue(string.Empty, "Application");
        appClassKey?.SetValue("AppUserModelID", AppVersion.AppUserModelId);
        appClassKey?.SetValue("ApplicationCompany", AppVersion.CompanyName);
        appClassKey?.SetValue("ApplicationName", AppVersion.ProductName);
        appClassKey?.SetValue("ApplicationIcon", icon);
        using var appOpenKey = appClassKey?.CreateSubKey(@"shell\open\command");
        appOpenKey?.SetValue(string.Empty, openCommand);
    }

    private static void RegisterFileAssociations(string exePath)
    {
        var openCommand = $"\"{exePath}\" \"%1\"";
        var icon = $"\"{exePath}\",0";
        var extensions = MediaFileHelper.VideoExtensions
            .Concat(MediaFileHelper.AudioExtensions)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var extension in extensions)
        {
            var progId = $"{AppRegistryName}{extension}";
            var typeName = MediaFileHelper.IsAudioFile($"file{extension}")
                ? $"{AppVersion.ProductName} audio"
                : $"{AppVersion.ProductName} video";

            using var progIdKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}");
            progIdKey?.SetValue(string.Empty, typeName);
            progIdKey?.SetValue("FriendlyTypeName", typeName);
            progIdKey?.SetValue("ApplicationCompany", AppVersion.CompanyName);
            progIdKey?.SetValue("ApplicationName", AppVersion.ProductName);
            progIdKey?.SetValue("DefaultIcon", icon);
            using var progOpenKey = progIdKey?.CreateSubKey(@"shell\open\command");
            progOpenKey?.SetValue(string.Empty, openCommand);

            using var extensionKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            using var openWithKey = extensionKey?.CreateSubKey(@"OpenWithProgids");
            openWithKey?.SetValue(progId, string.Empty);
        }
    }

    private static void RegisterDefaultAppCapabilities()
    {
        using var capabilitiesKey = Registry.CurrentUser.CreateSubKey(CapabilitiesKey);
        capabilitiesKey?.SetValue("ApplicationName", AppVersion.ProductName);
        capabilitiesKey?.SetValue("ApplicationDescription", $"{AppVersion.ProductName} media player");
        capabilitiesKey?.SetValue("ApplicationIcon", AppVersion.ProductName);

        using var fileAssociationsKey = Registry.CurrentUser.CreateSubKey(FileAssociationsKey);
        foreach (var extension in MediaFileHelper.VideoExtensions
                     .Concat(MediaFileHelper.AudioExtensions)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            fileAssociationsKey?.SetValue(extension, $"{AppRegistryName}{extension}");
        }

        using var registeredAppsKey = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications");
        registeredAppsKey?.SetValue(AppRegistryName, CapabilitiesKey);
    }

    private static void NotifyAssociationChanged()
    {
        const int shcneAssocChanged = 0x08000000;
        const uint shcnfIdList = 0x0000;
        SHChangeNotify(shcneAssocChanged, shcnfIdList, nint.Zero, nint.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
}
