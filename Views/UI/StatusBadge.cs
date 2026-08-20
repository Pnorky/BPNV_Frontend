using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Converters;

namespace AvaloniaApp.Views.UI;

public sealed class StatusBadge : Border
{
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<StatusBadge, string>(nameof(Status), string.Empty);

    private static readonly StatusToColorConverter BackgroundConverter = new();
    private static readonly StatusToForegroundConverter ForegroundConverter = new();
    private readonly Border _dot;
    private readonly TextBlock _label;

    public StatusBadge()
    {
        CornerRadius = new CornerRadius(999);
        Padding = new Thickness(12, 5);
        HorizontalAlignment = HorizontalAlignment.Left;
        _dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
        _label = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 0.5,
            VerticalAlignment = VerticalAlignment.Center
        };
        Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _dot, _label } };
        UpdateAppearance();
        ActualThemeVariantChanged += (_, _) => UpdateAppearance();
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty) UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (_label is null || _dot is null) return;
        _label.Text = Status;
        Background = BackgroundConverter.Convert(Status, typeof(IBrush), null, CultureInfo.CurrentCulture) as IBrush;
        var foreground = ForegroundConverter.Convert(Status, typeof(IBrush), null, CultureInfo.CurrentCulture) as IBrush;
        _label.Foreground = foreground;
        _dot.Background = foreground;
    }
}
