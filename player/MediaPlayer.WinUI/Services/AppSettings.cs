namespace MediaPlayer.Services;

public class AppSettings
{
    public int Volume { get; set; } = 50;
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 720;
    public double WindowLeft { get; set; } = double.NaN;
    public double WindowTop { get; set; } = double.NaN;
    public bool IsPlaylistVisible { get; set; }
    public bool LockWindowToVideoAspect { get; set; }
    public string VideoFitMode { get; set; } = "Fit";
    public string RepeatMode { get; set; } = "Off";
    public List<string> RecentFiles { get; set; } = [];
}
