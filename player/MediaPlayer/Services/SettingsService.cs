using System.IO;
using System.Text.Json;

namespace MediaPlayer.Services;

public class SettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MediaUtilities",
        "player");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void AddRecentFile(AppSettings settings, string filePath)
    {
        settings.RecentFiles.RemoveAll(f =>
            string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));

        settings.RecentFiles.Insert(0, filePath);

        if (settings.RecentFiles.Count > 10)
            settings.RecentFiles = settings.RecentFiles.Take(10).ToList();
    }
}
