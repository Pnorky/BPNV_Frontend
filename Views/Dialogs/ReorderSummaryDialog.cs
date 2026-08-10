using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Views.Dialogs;

public class ReorderSummaryDialog : Window
{
    public ReorderSummaryDialog()
    {
        Title = "Order Summary - BPNV Convenience Store";
        Width = 860;
        Height = 640;
        MinWidth = 720;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Background");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = "Supplier order summary" };
        title.Classes.Add("h1");
        var description = new TextBlock
        {
            Text = "Suggested quantities follow each product's critical and warning reorder rule."
        };
        description.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var header = new StackPanel { Spacing = 4, Children = { title, description } };

        var summaries = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<SupplierOrderSummary>((summary, _) => CreateSupplierSummary(summary!), true)
        };
        summaries.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(InventoryViewModel.OrderSummaries)));
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = summaries
        };
        Grid.SetRow(scroll, 1);

        var note = new TextBlock
        {
            Text = "Review quantities before placing orders with suppliers.",
            VerticalAlignment = VerticalAlignment.Center
        };
        note.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var close = new Button { Content = "Close", Padding = new Thickness(22, 8) };
        close.Classes.Add("primary");
        close.Click += OnCloseClick;
        Grid.SetColumn(close, 1);
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { note, close }
        };
        Grid.SetRow(footer, 2);
        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(26),
            RowSpacing = 18,
            Children = { header, scroll, footer }
        };
    }

    private static Control CreateSupplierSummary(SupplierOrderSummary summary)
    {
        var supplier = new TextBlock { FontSize = 18, FontWeight = FontWeight.SemiBold };
        supplier.Bind(TextBlock.TextProperty, new Binding(nameof(SupplierOrderSummary.SupplierName)));
        var total = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        total.Bind(TextBlock.TextProperty, new Binding(nameof(SupplierOrderSummary.Summary)));
        total.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        Grid.SetColumn(total, 1);

        var columnHeader = CreateColumns(
            CreateHeader("PRODUCT"), CreateHeader("SKU"), CreateHeader("ON HAND"),
            CreateHeader("TIER"), CreateHeader("CRITICAL"), CreateHeader("WARNING"), CreateHeader("ORDER"));
        var headerBorder = new Border { Padding = new Thickness(12, 8), Child = columnHeader };
        headerBorder.BindResource(Border.BackgroundProperty, "Muted");

        var products = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ReorderProductSummary>((product, _) => CreateProductRow(product!), true)
        };
        products.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(SupplierOrderSummary.Products)));
        var card = new Border
        {
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 14),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { supplier, total } },
                    headerBorder,
                    products
                }
            }
        };
        card.Classes.Add("theme-card");
        card.BindResource(Border.BackgroundProperty, "Card");
        return card;
    }

    private static Control CreateProductRow(ReorderProductSummary product)
    {
        var name = BoundText(nameof(ReorderProductSummary.Name));
        name.FontWeight = FontWeight.SemiBold;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var quantity = BoundText(nameof(ReorderProductSummary.OrderQuantity));
        quantity.FontWeight = FontWeight.Bold;
        quantity.BindResource(TextBlock.ForegroundProperty, "Primary");
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(12, 9),
            Child = CreateColumns(name, BoundText(nameof(ReorderProductSummary.Sku)),
                BoundText(nameof(ReorderProductSummary.OnHand)), BoundText(nameof(ReorderProductSummary.Tier)),
                BoundText(nameof(ReorderProductSummary.CriticalLevel)), BoundText(nameof(ReorderProductSummary.WarningLevel)), quantity)
        };
        border.BindResource(Border.BorderBrushProperty, "Border");
        return border;
    }

    private static Grid CreateColumns(params TextBlock[] cells)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.8*,0.8*,0.7*,0.7*,0.7*,0.7*,0.8*"),
            ColumnSpacing = 12
        };
        for (var index = 0; index < cells.Length; index++)
        {
            Grid.SetColumn(cells[index], index);
            grid.Children.Add(cells[index]);
        }
        return grid;
    }

    private static TextBlock CreateHeader(string text) =>
        new() { Text = text, FontSize = 10, FontWeight = FontWeight.SemiBold };

    private static TextBlock BoundText(string path)
    {
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new Binding(path));
        return text;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
