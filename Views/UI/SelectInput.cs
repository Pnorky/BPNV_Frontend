using Avalonia.Controls;
using Avalonia.Layout;

namespace AvaloniaApp.Views.UI;

public sealed class SelectInput : StackPanel
{
    private readonly TextBlock _label;
    private readonly ComboBox _combo;

    public SelectInput(string label, IEnumerable<string>? options = null)
    {
        Spacing = 7;
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _label = new TextBlock { Text = label.ToUpperInvariant() };
        _label.Classes.Add("form-label");

        _combo = new ComboBox
        {
            MinHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _combo.Classes.Add("form-select");
        if (options is not null)
            _combo.ItemsSource = options;

        Children.Add(_label);
        Children.Add(_combo);
    }

    public string? Label
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public IEnumerable<string>? Options
    {
        get => _combo.ItemsSource?.Cast<string>();
        set => _combo.ItemsSource = value;
    }

    public object? SelectedItem
    {
        get => _combo.SelectedItem;
        set => _combo.SelectedItem = value;
    }

    public ComboBox Combo => _combo;
}
