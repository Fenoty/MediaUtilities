namespace MediaPlayer.ViewModels;

public sealed class PlaybackRateItem(float rate)
{
    public float Rate { get; } = rate;

    public string Label => Rate % 1 == 0
        ? $"{Rate.ToString("0", System.Globalization.CultureInfo.InvariantCulture)}x"
        : $"{Rate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}x";

    public override string ToString() => Label;
}
