using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaApp.Views.UI;

public sealed class AnimatedMenuIcon : Grid
{
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(200);
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<AnimatedMenuIcon, bool>(nameof(IsOpen));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<AnimatedMenuIcon>();
    private readonly Border _topBar;
    private readonly Border _middleBar;
    private readonly Border _bottomBar;
    private readonly ScaleTransform _topScale = new();
    private readonly RotateTransform _topRotation = new();
    private readonly TranslateTransform _topTranslation = new();
    private readonly ScaleTransform _bottomScale = new();
    private readonly RotateTransform _bottomRotation = new();
    private readonly TranslateTransform _bottomTranslation = new();

    public AnimatedMenuIcon()
    {
        Width = 20; Height = 20; IsHitTestVisible = false;
        ConfigureTransition(_topScale, ScaleTransform.ScaleXProperty); ConfigureTransition(_topRotation, RotateTransform.AngleProperty);
        ConfigureTransition(_topTranslation, TranslateTransform.XProperty); ConfigureTransition(_topTranslation, TranslateTransform.YProperty);
        ConfigureTransition(_bottomScale, ScaleTransform.ScaleXProperty); ConfigureTransition(_bottomRotation, RotateTransform.AngleProperty);
        ConfigureTransition(_bottomTranslation, TranslateTransform.XProperty); ConfigureTransition(_bottomTranslation, TranslateTransform.YProperty);
        _topBar = CreateBar(new TransformGroup { Children = { _topScale, _topRotation, _topTranslation } });
        _middleBar = CreateBar();
        _bottomBar = CreateBar(new TransformGroup { Children = { _bottomScale, _bottomRotation, _bottomTranslation } });
        Children.Add(_topBar); Children.Add(_middleBar); Children.Add(_bottomBar);
        UpdateVisualState();
    }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty) UpdateVisualState(); else if (change.Property == ForegroundProperty) UpdateForeground();
    }

    private static void ConfigureTransition(Animatable transform, AvaloniaProperty property) => transform.Transitions = new Transitions
    {
        new DoubleTransition { Property = property, Duration = AnimationDuration }
    };

    private static Border CreateBar(ITransform? transform = null) => new()
    {
        Width = 18, Height = 2, CornerRadius = new CornerRadius(1), HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, RenderTransformOrigin = RelativePoint.Center, RenderTransform = transform
    };

    private void UpdateVisualState()
    {
        _topScale.ScaleX = IsOpen ? 0.6 : 1; _topTranslation.X = IsOpen ? -5.2 : 0; _topTranslation.Y = IsOpen ? -3.8 : -6; _topRotation.Angle = IsOpen ? -45 : 0;
        _bottomScale.ScaleX = IsOpen ? 0.6 : 1; _bottomTranslation.X = IsOpen ? -5.2 : 0; _bottomTranslation.Y = IsOpen ? 3.8 : 6; _bottomRotation.Angle = IsOpen ? 45 : 0;
    }

    private void UpdateForeground() => _topBar.Background = _middleBar.Background = _bottomBar.Background = Foreground;
}
