using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;

namespace AvaloniaApp.Views;

public class ProductCatalogView : UserControl
{
    public ProductCatalogView()
    {
        var title = new TextBlock { Text = "Product catalog" }; title.Classes.Add("h1");
        var search = new TextBox { PlaceholderText = "Search database product, SKU, supplier, category, or barcode..." };
        search.Classes.Add("search");
        search.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var refresh = new Button { Content = "Refresh" }; refresh.Classes.Add("secondary"); refresh.Bind(Button.CommandProperty, new Binding("LoadCommand"));
        Grid.SetColumn(refresh, 1);
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10, Children = { search, refresh } };

        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredProducts"));
        list.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) => ProductRow(), true);
        var card = new Border { Padding = new Thickness(0), ClipToBounds = true, Child = list }; card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            RowSpacing = 14,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, Muted("Database-backed products and current piece balances") } },
                At(Status(), 1), At(toolbar, 2), At(card, 3)
            }
        };
    }

    private static Control ProductRow()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.4*,1*,0.8*,1*,0.8*"),
            ColumnSpacing = 14,
            Children =
            {
                new StackPanel { Children = { Text("Name", FontWeight.SemiBold), Text("Sku", resource: "MutedForeground") } },
                At(new StackPanel { Children = { Text("SupplierName"), Text("Category", resource: "MutedForeground") } }, column: 1),
                At(Text("ItemType", vertical: true), column: 2),
                At(Text("StockDisplay", vertical: true), column: 3),
                At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text("PriceDisplay", FontWeight.SemiBold), Text("StockStatus", resource: "MutedForeground") } }, column: 4)
            }
        };
        var border = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(16, 12), Child = row };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Text("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Text(string path, FontWeight? weight = null, string? resource = null, bool vertical = false) { var value = new TextBlock { FontWeight = weight ?? FontWeight.Normal, VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Top }; value.Bind(TextBlock.TextProperty, new Binding(path)); if (resource is not null) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(resource)); return value; }
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control { Grid.SetRow(value, row); Grid.SetColumn(value, column); return value; }
}
