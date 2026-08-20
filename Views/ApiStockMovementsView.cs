using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class ApiStockMovementsView : UserControl
{
    public ApiStockMovementsView()
    {
        var product = new SearchableSelect { PlaceholderText = "Select product" };
        product.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Products"));
        product.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedProduct"));
        product.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) =>
            new StackPanel { Children = { Text("Name"), Text("StockDisplay", true) } }, true);
        var search = new IconInput("Search", "Search product, SKU, or supplier...");
        search.Input.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var quantity = new NumberField { Minimum = 1, Increment = 1, FormatString = "0" };
        quantity.Bind(NumberField.ValueProperty, new Binding("Quantity"));
        var reference = Input("Reference", "Optional reference");
        var notes = Input("Notes", "Optional notes");
        var transfer = new ActionButton("Move to Display");
        transfer.Bind(Button.CommandProperty, new Binding("TransferCommand"));
        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.8*,1*,1.2*,1.5*,Auto"), ColumnSpacing = 12,
            Children = { Field("PRODUCT", product), At(Field("QUANTITY", quantity), 1), At(Field("REFERENCE", reference), 2), At(Field("NOTES", notes), 3), At(transfer, 4) }
        };
        var root = new StackPanel
        {
            Margin = new Thickness(30), Spacing = 14,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { Heading("Stock movements"), Muted("Move API-backed Bodega stock to Display.") } },
                Status(),
                Field("SEARCH PRODUCTS", search),
                Card(form),
                new TextBlock { Text = "Select a product above to view its current Bodega balance before transferring stock." }
            }
        };
        Content = new ScrollViewer { Content = root };
    }

    private static TextBox Input(string path, string placeholder) { var value = new TextBox { PlaceholderText = placeholder }; value.Classes.Add("form-input"); value.Bind(TextBox.TextProperty, new Binding(path)); return value; }
    private static StackPanel Field(string label, Control control) { var caption = new TextBlock { Text = label }; caption.Classes.Add("form-label"); return new StackPanel { Spacing = 5, Children = { caption, control } }; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), Child = Bound("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h1"); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Bound(string path) { var value = new TextBlock(); value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static TextBlock Text(string path, bool muted = false) { var value = Bound(path); if (muted) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
}
