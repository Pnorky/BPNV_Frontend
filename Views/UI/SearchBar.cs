using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace AvaloniaApp.Views.UI;

public sealed class SearchBar : Border
{
    private readonly TextBox _input;
    private readonly Button _searchButton;

    public SearchBar(string placeholder = "Filter documents...", string buttonText = "Search")
    {
        Classes.Add("form-search");
        CornerRadius = new CornerRadius(8);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(6);

        _input = new TextBox
        {
            PlaceholderText = placeholder,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        _searchButton = new ActionButton(buttonText, ActionButtonVariant.Secondary);

        Child = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 6,
            Children = { _input, _searchButton }
        };
        Grid.SetColumn(_searchButton, 1);
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

    public Button SearchButton => _searchButton;
}
