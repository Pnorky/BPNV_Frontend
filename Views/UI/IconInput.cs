using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaApp.Views.Controls;

namespace AvaloniaApp.Views.UI;

public sealed class IconInput : Grid
{
    private readonly TextBox _input;
    private readonly HomisIcon _icon;

    public IconInput(string kind, string? placeholder = null)
    {
        _icon = new HomisIcon
        {
            Kind = kind,
            Width = 20,
            Height = 20,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };
        _icon.Classes.Add("form-input-icon");

        _input = new TextBox
        {
            PlaceholderText = placeholder,
            Padding = new Thickness(36, 0, 12, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _input.Classes.Add("form-input");

        Children.Add(_input);
        Children.Add(_icon);
    }

    public string Kind
    {
        get => _icon.Kind;
        set => _icon.Kind = value;
    }

    public string? Text
    {
        get => _input.Text;
        set => _input.Text = value;
    }

    public string? Placeholder
    {
        get => _input.PlaceholderText;
        set => _input.PlaceholderText = value;
    }

    public TextBox Input => _input;
}
