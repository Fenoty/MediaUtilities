using Microsoft.UI.Input;
using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Controls.Primitives;

using Microsoft.UI.Xaml.Input;

using Microsoft.UI.Xaml.Media;



namespace MediaPlayer.Controls;



public class RoundThumbSlider : Slider

{

    public RoundThumbSlider()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);

        Loaded += (_, _) => ApplyThumb();

        ValueChanged += (_, _) => ApplyThumb();

        SizeChanged += (_, _) => ApplyThumb();

        PointerEntered += (_, _) => ApplyThumb();

        PointerExited += (_, _) => ApplyThumb();

        GotFocus += (_, _) => ApplyThumb();

        LostFocus += (_, _) => ApplyThumb();

    }



    private void ApplyThumb()

    {

        Margin = new Thickness(0);

        Padding = new Thickness(0);



        var thumb = FindDescendant<Thumb>(this);

        if (thumb is null)
            return;



        var thumbSize = Application.Current.Resources["ThumbSize"] is double size ? size : 10d;

        thumb.Width = thumbSize;

        thumb.Height = thumbSize;

        thumb.MinWidth = thumbSize;

        thumb.MinHeight = thumbSize;

        thumb.Padding = new Thickness(0);

        thumb.Margin = new Thickness(0);



        if (Application.Current.Resources["RoundThumbTemplate"] is ControlTemplate template)

            thumb.Template = template;



        RemoveHorizontalInsets(this);

        RemoveSliderThumbInsets(this);

        LockTrackColors();

    }



    private void LockTrackColors()

    {

        if (Application.Current.Resources["SliderTrackBrush"] is not Brush trackBrush ||

            Application.Current.Resources["SliderFillBrush"] is not Brush fillBrush)

            return;



        Background = trackBrush;

        Foreground = fillBrush;



        var track = FindDescendantByName<Microsoft.UI.Xaml.Shapes.Rectangle>(this, "HorizontalTrackRect");

        var fill = FindDescendantByName<Microsoft.UI.Xaml.Shapes.Rectangle>(this, "HorizontalDecreaseRect");

        if (track is not null)

            track.Fill = trackBrush;

        if (fill is not null)

            fill.Fill = fillBrush;

    }



    private static void RemoveSliderThumbInsets(DependencyObject root)

    {

        if (root is Grid grid)

        {

            foreach (var column in grid.ColumnDefinitions)

            {

                if (column.Width.IsAbsolute && column.Width.Value is > 0 and <= 12)

                    column.Width = new GridLength(0);

            }

        }



        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)

            RemoveSliderThumbInsets(VisualTreeHelper.GetChild(root, i));

    }



    private static void RemoveHorizontalInsets(DependencyObject root)

    {

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)

        {

            var child = VisualTreeHelper.GetChild(root, i);

            if (child is FrameworkElement element and not Thumb)

            {

                element.Margin = new Thickness(0);

                if (element is Border border)

                    border.Padding = new Thickness(0);

                else if (element is Control control)

                    control.Padding = new Thickness(0);

            }



            RemoveHorizontalInsets(child);

        }

    }



    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject

    {

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)

        {

            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match)

                return match;



            var nested = FindDescendant<T>(child);

            if (nested is not null)

                return nested;

        }



        return null;

    }



    private static T? FindDescendantByName<T>(DependencyObject root, string name) where T : FrameworkElement

    {

        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var i = 0; i < count; i++)

        {

            var child = VisualTreeHelper.GetChild(root, i);

            if (child is T match && match.Name == name)

                return match;



            var nested = FindDescendantByName<T>(child, name);

            if (nested is not null)

                return nested;

        }



        return null;

    }

}


