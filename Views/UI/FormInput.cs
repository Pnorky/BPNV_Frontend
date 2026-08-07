using Avalonia.Controls;
using Avalonia.Layout;

namespace AvaloniaApp.Views.UI;

public sealed class FormInput : StackPanel
{
    private readonly TextBlock _label;
    private readonly TextBox _input;

    public FormInput(string label, string? placeholder = null)
    {
        Spacing = 7;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _label = new TextBlock { Text = label.ToUpperInvariant() };
        _label.Classes.Add("form-label");

        _input = new TextBox
        {
            PlaceholderText = placeholder,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _input.Classes.Add("form-input");

        Children.Add(_label);
        Children.Add(_input);
    }

    public string? Label
    {
        get => _label.Text;
        set => _label.Text = value;
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
