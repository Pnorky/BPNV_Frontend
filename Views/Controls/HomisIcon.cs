using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace AvaloniaApp.Views.Controls;

public sealed class HomisIcon : Control
{
    private const double RawIconSize = 24;

    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<HomisIcon, string>(nameof(Kind), "Box");

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<HomisIcon>();

    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<HomisIcon, double>(nameof(StrokeWidth), 1.8);

    static HomisIcon()
    {
        AffectsRender<HomisIcon>(KindProperty, ForegroundProperty, StrokeWidthProperty);
    }

    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Foreground is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var scale = Math.Min(Bounds.Width, Bounds.Height) / RawIconSize;
        var offsetX = (Bounds.Width - RawIconSize * scale) / 2;
        var offsetY = (Bounds.Height - RawIconSize * scale) / 2;
        var pen = new Pen(Foreground, StrokeWidth, null, PenLineCap.Round, PenLineJoin.Round);

        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        using (context.PushTransform(Matrix.CreateTranslation(offsetX, offsetY)))
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
            context.DrawGeometry(null, pen, EmbeddedLucideGeometry.Get(Kind));
    }
}
