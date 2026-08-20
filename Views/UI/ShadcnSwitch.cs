using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Views.Helper;

namespace AvaloniaApp.Views.UI;

public sealed class ShadcnSwitch : ToggleButton
{
    private readonly Border _track;
    private readonly Border _thumb;
    private readonly TranslateTransform _thumbTransform = new();

    public ShadcnSwitch()
    {
        Width = 36; Height = 20; MinWidth = 36; MinHeight = 20;
        Padding = new Thickness(0); BorderThickness = new Thickness(0);
        Background = Brushes.Transparent; Cursor = new Cursor(StandardCursorType.Hand);
        _thumbTransform.Transitions = new Transitions
        {
            new DoubleTransition { Property = TranslateTransform.XProperty, Duration = TimeSpan.FromMilliseconds(150) }
        };
        _thumb = new Border
        {
            Width = 16, Height = 16, Margin = new Thickness(2), CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = _thumbTransform
        };
        _track = new Border { Width = 36, Height = 20, CornerRadius = new CornerRadius(10), Child = _thumb };
        _track.BorderThickness = new Thickness(1);
        Template = new FuncControlTemplate<ShadcnSwitch>((_, _) => _track);
        IsCheckedChanged += (_, _) => ApplyVisualState();
        ActualThemeVariantChanged += (_, _) => ApplyVisualState();
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        _track.Background = IsChecked == true
            ? AppTheme.Brush("Primary", Brushes.SlateBlue)
            : AppTheme.Brush("SecondaryForeground", Brushes.Gray);
        _track.BorderBrush = IsChecked == true
            ? AppTheme.Brush("Primary", Brushes.SlateBlue)
            : AppTheme.Brush("Border", Brushes.DarkGray);
        _thumb.Background = AppTheme.Brush("Card", Brushes.White);
        _thumbTransform.X = IsChecked == true ? 16 : 0;
    }
}
