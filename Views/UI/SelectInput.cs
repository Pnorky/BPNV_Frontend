using Avalonia.Controls;
using Avalonia.Layout;

namespace AvaloniaApp.Views.UI;

public sealed class SelectInput : StackPanel
{
    private readonly TextBlock _label;
    private readonly SelectDropdown _dropdown;

    public SelectInput(string label, IEnumerable<string>? options = null)
    {
        Spacing = 7;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _label = new TextBlock { Text = label.ToUpperInvariant() };
        _label.Classes.Add("form-label");

        _dropdown = new SelectDropdown
        {
            MinHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        if (options is not null)
            _dropdown.ItemsSource = options;

        Children.Add(_label);
        Children.Add(_dropdown);
    }

    public string? Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public IEnumerable<string>? Options
    {
        get => _dropdown.ItemsSource?.Cast<string>();
        set => _dropdown.ItemsSource = value;
    }

    public object? SelectedItem
    {
        get => _dropdown.SelectedItem;
        set => _dropdown.SelectedItem = value;
    }

    public SelectDropdown Dropdown => _dropdown;
}
