using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Views.Helper;

namespace AvaloniaApp.Views.UI;

public sealed class Avatar : Grid
{
    public static readonly StyledProperty<IImage?> SourceProperty = AvaloniaProperty.Register<Avatar, IImage?>(nameof(Source));
    public static readonly StyledProperty<string> FallbackProperty = AvaloniaProperty.Register<Avatar, string>(nameof(Fallback), string.Empty);
    public static readonly StyledProperty<double> AvatarSizeProperty = AvaloniaProperty.Register<Avatar, double>(nameof(AvatarSize), 40);
    public static readonly StyledProperty<bool> ShowBadgeProperty = AvaloniaProperty.Register<Avatar, bool>(nameof(ShowBadge));
    private readonly Border _frame;
    private readonly Image _image;
    private readonly TextBlock _fallback;
    private readonly Border _badge;

    public Avatar()
    {
        _image = new Image { Stretch = Stretch.UniformToFill };
        _fallback = new TextBlock { FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _frame = new Border { BorderThickness = new Thickness(1), ClipToBounds = true, Child = new Grid { Children = { _fallback, _image } } };
        _badge = new Border
        {
            Width = 11, Height = 11, CornerRadius = new CornerRadius(999), BorderThickness = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, IsVisible = false
        };
        Children.Add(_frame); Children.Add(_badge);
        UpdateAppearance();
        ActualThemeVariantChanged += (_, _) => UpdateTheme();
    }

    public IImage? Source { get => GetValue(SourceProperty); set => SetValue(SourceProperty, value); }
    public string Fallback { get => GetValue(FallbackProperty); set => SetValue(FallbackProperty, value); }
    public double AvatarSize { get => GetValue(AvatarSizeProperty); set => SetValue(AvatarSizeProperty, value); }
    public bool ShowBadge { get => GetValue(ShowBadgeProperty); set => SetValue(ShowBadgeProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty || change.Property == FallbackProperty || change.Property == AvatarSizeProperty || change.Property == ShowBadgeProperty)
            UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        if (_frame is null || _image is null || _fallback is null || _badge is null) return;
        Width = AvatarSize; Height = AvatarSize; _frame.CornerRadius = new CornerRadius(AvatarSize / 2);
        _image.Source = Source; _image.IsVisible = Source is not null;
        _fallback.Text = Fallback; _fallback.FontSize = Math.Max(10, AvatarSize * 0.34); _fallback.IsVisible = Source is null;
        _badge.IsVisible = ShowBadge;
        UpdateTheme();
    }

    private void UpdateTheme()
    {
        if (_frame is null || _fallback is null || _badge is null) return;
        _frame.Background = AppTheme.Brush("Muted", Brushes.LightGray, ActualThemeVariant);
        _frame.BorderBrush = AppTheme.Brush("Border", Brushes.LightGray, ActualThemeVariant);
        _fallback.Foreground = AppTheme.Brush("MutedForeground", Brushes.DimGray, ActualThemeVariant);
        _badge.Background = AppTheme.Brush("SuccessGreen", Brushes.Green, ActualThemeVariant);
        _badge.BorderBrush = AppTheme.Brush("Background", Brushes.White, ActualThemeVariant);
    }
}
