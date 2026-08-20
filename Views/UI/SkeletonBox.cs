using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaApp.Views.Helper;

namespace AvaloniaApp.Views.UI;

public class SkeletonBox : Border
{
    public SkeletonBox()
    {
        CornerRadius = new CornerRadius(4);
        Background = AppTheme.Brush("Secondary", Brushes.LightGray, ActualThemeVariant);
        Opacity = 0.55;
        Styles.Add(new Style(selector => selector.OfType<SkeletonBox>())
        {
            Animations =
            {
                new Avalonia.Animation.Animation
                {
                    Duration = TimeSpan.FromMilliseconds(1100),
                    IterationCount = IterationCount.Infinite,
                    PlaybackDirection = PlaybackDirection.Alternate,
                    Children =
                    {
                        new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.35) } },
                        new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 0.9) } }
                    }
                }
            }
        });
        ActualThemeVariantChanged += (_, _) => Background = AppTheme.Brush("Secondary", Brushes.LightGray, ActualThemeVariant);
    }
}

public sealed class SkeletonText : SkeletonBox
{
    public SkeletonText()
    {
        Height = 12;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
    }
}
