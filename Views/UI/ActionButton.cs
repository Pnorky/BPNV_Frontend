using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaApp.Views.Controls;

namespace AvaloniaApp.Views.UI;

public enum ActionButtonVariant
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

public enum ActionButtonSize
{
    Sm,
    Md,
    Lg
}

public sealed class ActionButton : Button
{
    private static readonly string[] VariantClasses = ["primary", "secondary", "ghost", "danger"];

    protected override Type StyleKeyOverride => typeof(Button);

    private string _text = string.Empty;
    private string? _icon;
    private readonly double _iconSize;
    private readonly double _contentSpacing;

    public ActionButton(string text, ActionButtonVariant variant = ActionButtonVariant.Primary, string? icon = null)
        : this(text, variant, ActionButtonSize.Md, icon)
    {
    }

    public ActionButton(string text, ActionButtonVariant variant, ActionButtonSize size, string? icon = null)
    {
        Text = text;
        Variant = variant;
        Size = size;
        Icon = icon;
        Classes.Add(VariantClasses[(int)variant]);
        Classes.Add($"size-{size.ToString().ToLowerInvariant()}");
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        (_iconSize, _contentSpacing) = ApplySize(size);
        RebuildContent();
    }

    public ActionButtonVariant Variant { get; }
    public ActionButtonSize Size { get; }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            RebuildContent();
        }
    }

    public string? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            RebuildContent();
        }
    }

    private void RebuildContent()
    {
        if (string.IsNullOrEmpty(_icon))
        {
            Content = _text;
            return;
        }

        var icon = new HomisIcon { Kind = _icon, Width = _iconSize, Height = _iconSize };
        var label = new TextBlock { Text = _text, VerticalAlignment = VerticalAlignment.Center };
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = _contentSpacing,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, label }
        };
    }

    private (double IconSize, double ContentSpacing) ApplySize(ActionButtonSize size)
    {
        var metrics = size switch
        {
            ActionButtonSize.Sm => (MinHeight: 32d, Padding: new Avalonia.Thickness(10, 6), FontSize: 12d, IconSize: 16d, Spacing: 6d),
            ActionButtonSize.Lg => (MinHeight: 48d, Padding: new Avalonia.Thickness(22, 12), FontSize: 16d, IconSize: 20d, Spacing: 10d),
            _ => (MinHeight: 40d, Padding: new Avalonia.Thickness(18, 9), FontSize: 14d, IconSize: 18d, Spacing: 8d)
        };
        MinHeight = metrics.MinHeight;
        Padding = metrics.Padding;
        FontSize = metrics.FontSize;
        return (metrics.IconSize, metrics.Spacing);
    }
}
