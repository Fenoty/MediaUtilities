using MediaPlayer.Helpers;
using MediaPlayer.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace MediaPlayer.Controls;

public sealed class PlaybackRateSelector : HandButton
{
    public static readonly DependencyProperty RatesProperty =
        DependencyProperty.Register(nameof(Rates), typeof(IReadOnlyList<PlaybackRateItem>), typeof(PlaybackRateSelector),
            new PropertyMetadata(null, OnRatesChanged));

    public static readonly DependencyProperty SelectedRateProperty =
        DependencyProperty.Register(nameof(SelectedRate), typeof(PlaybackRateItem), typeof(PlaybackRateSelector),
            new PropertyMetadata(null, OnSelectedRateChanged));

    private readonly TextBlock _label = new()
    {
        FontSize = 11,
        VerticalAlignment = VerticalAlignment.Center,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly FontIcon _chevron = new()
    {
        Glyph = "\uE70D",
        FontSize = 8,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center
    };

    private readonly MenuFlyout _flyout = new();

    public IReadOnlyList<PlaybackRateItem>? Rates
    {
        get => (IReadOnlyList<PlaybackRateItem>?)GetValue(RatesProperty);
        set => SetValue(RatesProperty, value);
    }

    public PlaybackRateItem? SelectedRate
    {
        get => (PlaybackRateItem?)GetValue(SelectedRateProperty);
        set => SetValue(SelectedRateProperty, value);
    }

    public PlaybackRateSelector()
    {
        Style = (Style)Application.Current.Resources["PlaybackRateSelectorStyle"];

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(_label);
        row.Children.Add(_chevron);
        Content = row;
        Flyout = _flyout;
    }

    private static void OnRatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlaybackRateSelector selector)
            selector.RebuildFlyout();
    }

    private static void OnSelectedRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PlaybackRateSelector selector)
            selector.UpdateLabel();
    }

    private void RebuildFlyout()
    {
        _flyout.Items.Clear();
        if (Rates is null)
            return;

        foreach (var rate in Rates)
        {
            var item = new MenuFlyoutItem
            {
                Text = rate.Label,
                Tag = rate
            };
            item.Click += OnFlyoutItemClick;
            _flyout.Items.Add(item);
        }

        UpdateLabel();
    }

    private void UpdateLabel()
        => _label.Text = SelectedRate?.Label ?? "1x";

    private void OnFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: PlaybackRateItem rate })
            SelectedRate = rate;
    }
}
