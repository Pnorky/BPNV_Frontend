using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Lucide.Avalonia;

namespace AvaloniaApp.Views;

public class ReportsView : UserControl
{
    public ReportsView()
    {
        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(30),
            RowSpacing = 18,
            Children = { BuildHeader(), At(BuildTabs(), row: 1) }
        };
    }

    private static Control BuildHeader()
    {
        var title = Heading("Reports", "h1");
        var status = Muted(path: "ExportStatus", fontSize: 11);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                ExportButton("Export PDF", "ExportPdfCommand", LucideIconKind.FileText),
                ExportButton("Export Excel", "ExportExcelCommand", LucideIconKind.FileSpreadsheet),
                Button("Refresh", "RefreshCommand", true)
            }
        };
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, Muted("Sales, inventory, and supplier ordering summaries"), status } },
                At(actions, column: 1)
            }
        };
    }

    private static Button ExportButton(string text, string command, LucideIconKind kind)
    {
        var button = Button(null, command);
        button.Width = 164;
        button.Height = 44;
        button.Padding = new Thickness(12, 0);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.VerticalContentAlignment = VerticalAlignment.Center;
        button.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*"),
            RowDefinitions = new RowDefinitions("20"),
            ColumnSpacing = 7,
            Width = 140,
            Children =
            {
                new LucideIcon { Kind = kind, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                At(new TextBlock { Text = text, LineHeight = 20, VerticalAlignment = VerticalAlignment.Center }, column: 1)
            }
        };
        return button;
    }

    private static Control BuildTabs() => new TabControl
    {
        Items =
        {
            new TabItem { Header = "Sales Summary", Content = Scroll(BuildSales()) },
            new TabItem { Header = "Inventory Summary", Content = Scroll(BuildInventory()) },
            new TabItem { Header = "Order Summary", Content = Scroll(BuildOrders()) }
        }
    };

    private static Control BuildSales()
    {
        var content = PageStack();
        content.Children.Add(Stats(4,
            ("SALES TODAY", "TodaySalesDisplay"), ("GROSS SALES", "GrossSalesDisplay"),
            ("TRANSACTIONS", "Transactions"), ("UNITS SOLD", "UnitsSold")));

        var topProducts = new ItemsControl();
        Bind(topProducts, ItemsControl.ItemsSourceProperty, "TopProducts");
        topProducts.ItemTemplate = new FuncDataTemplate<ViewModels.ProductSalesSummary>((_, _) =>
        {
            var quantity = BoundText("Quantity", "{0} sold");
            Resource(quantity, TextBlock.ForegroundProperty, "MutedForeground");
            var sales = BoundText("SalesDisplay"); sales.FontWeight = FontWeight.Bold; sales.Width = 90; sales.TextAlignment = TextAlignment.Right;
            return RowBorder(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 20,
                Children = { Ellipsis("Product", true), At(quantity, column: 1), At(sales, column: 2) }
            }, new Thickness(0, 13), new Thickness(0, 1, 0, 0));
        }, true);

        var recentSales = new ItemsControl();
        Bind(recentSales, ItemsControl.ItemsSourceProperty, "RecentSales");
        recentSales.ItemTemplate = new FuncDataTemplate<SaleRecord>((_, _) => RowBorder(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.8*,1*,0.8*,0.8*"),
            Children =
            {
                new StackPanel { Children = { SemiBold("SaleNumber"), Muted(path: "TimeDisplay", fontSize: 10) } },
                At(Cell("CustomerType"), column: 1), At(Cell("ItemCount"), column: 2), At(Cell("TotalDisplay", true, TextAlignment.Right), column: 3)
            }
        }, new Thickness(4, 11)), true);

        var lower = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.9*,1.1*"), ColumnSpacing = 18,
            Children =
            {
                Card(new StackPanel { Spacing = 15, Children = { Heading("Top-selling products", "h2"), topProducts } }, new Thickness(22), VerticalAlignment.Top),
                At(Card(new StackPanel
                {
                    Spacing = 15,
                    Children = { Heading("Recent sales", "h2"), SmallHeader("0.8*,1*,0.8*,0.8*", "SALE", "PRICE TYPE", "ITEMS", "TOTAL"), recentSales }
                }, new Thickness(22), VerticalAlignment.Top), column: 1)
            }
        };
        content.Children.Add(lower);
        return content;
    }

    private static Control BuildInventory()
    {
        var content = PageStack();
        content.Children.Add(Stats(4,
            ("TOTAL UNITS", "TotalInventoryUnits"), ("DISPLAY", "DisplayUnits"),
            ("BODEGA", "BodegaUnits"), ("SELLING VALUE", "InventoryValueDisplay")));

        var summary = At(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 }, column: 1);
        summary.Children.Add(BoundText("MerchandiseCount", "{0} merchandise"));
        summary.Children.Add(BoundText("ConsumableCount", "{0} consumables"));
        summary.Children.Add(BoundText("SupplyCount", "{0} supplies"));
        var low = BoundText("LowStockItems", "{0} low stock"); low.FontWeight = FontWeight.SemiBold; summary.Children.Add(low);

        var items = new ItemsControl();
        Bind(items, ItemsControl.ItemsSourceProperty, "InventoryItems");
        items.ItemTemplate = new FuncDataTemplate<ProductItem>((_, _) => RowBorder(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.6*,1.1*,0.8*,0.6*,0.6*,0.6*,0.9*"), ColumnSpacing = 12,
            Children =
            {
                new StackPanel { Children = { SemiBold("Name"), Muted(path: "Sku", fontSize: 10) } },
                At(Ellipsis("SupplierName", verticalCenter: true), column: 1), At(Cell("ItemTypeDisplay"), column: 2),
                At(Cell("ShelfStock"), column: 3), At(Cell("BodegaStock"), column: 4),
                At(Cell("TotalStock", true), column: 5), At(Cell("StockStatus"), column: 6)
            }
        }, new Thickness(16, 10)), true);

        var table = new StackPanel
        {
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 16),
                    Children = { Heading("Inventory by location", "h2"), summary }
                },
                SmallHeader("1.6*,1.1*,0.8*,0.6*,0.6*,0.6*,0.9*", "PRODUCT", "SUPPLIER", "TYPE", "DISPLAY", "BODEGA", "TOTAL", "STATUS", new Thickness(16, 9)),
                items
            }
        };
        content.Children.Add(Card(table, clip: true));
        return content;
    }

    private static Control BuildOrders()
    {
        var content = PageStack();
        content.Children.Add(Stats(3,
            ("SUPPLIERS", "SuppliersToOrder"), ("PRODUCTS TO ORDER", "ProductsToOrder"),
            ("SUGGESTED UNITS", "SuggestedOrderUnits")));

        var summaries = new ItemsControl();
        Bind(summaries, ItemsControl.ItemsSourceProperty, "OrderSummaries");
        summaries.ItemTemplate = new FuncDataTemplate<ViewModels.SupplierOrderSummary>((_, _) =>
        {
            var products = new ItemsControl();
            Bind(products, ItemsControl.ItemsSourceProperty, "Products");
            products.ItemTemplate = new FuncDataTemplate<ViewModels.ReorderProductSummary>((_, _) =>
            {
                var order = BoundText("OrderQuantity"); order.FontWeight = FontWeight.Bold; Resource(order, TextBlock.ForegroundProperty, "Primary");
                return RowBorder(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.8*,0.8*,0.7*,0.7*,0.7*,0.8*"), ColumnSpacing = 12,
                    Children =
                    {
                        SemiBold("Name"), At(BoundText("Sku"), column: 1), At(BoundText("OnHand"), column: 2),
                        At(BoundText("ReorderLevel"), column: 3), At(BoundText("TargetStock"), column: 4), At(order, column: 5)
                    }
                }, new Thickness(12, 9));
            }, true);

            var supplier = BoundText("SupplierName"); supplier.FontSize = 18; supplier.FontWeight = FontWeight.SemiBold;
            return Card(new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { supplier, At(Muted(path: "Summary"), column: 1) } },
                    SmallHeader("1.8*,0.8*,0.7*,0.7*,0.7*,0.8*", "PRODUCT", "SKU", "ON HAND", "REORDER", "TARGET", "ORDER", new Thickness(12, 8)),
                    products
                }
            }, new Thickness(18), margin: new Thickness(0, 0, 0, 14));
        }, true);
        content.Children.Add(summaries);
        return content;
    }

    private static Grid Stats(int count, params (string Label, string Path)[] values)
    {
        var columns = string.Join(',', Enumerable.Repeat("*", count));
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = 14 };
        for (var index = 0; index < values.Length; index++)
        {
            var isSalesStat = values[index].Path is "TodaySalesDisplay" or "GrossSalesDisplay" or "Transactions" or "UnitsSold";
            var value = BoundText(values[index].Path); value.FontSize = 25; value.FontWeight = FontWeight.Bold;
            var card = Card(new StackPanel { Spacing = isSalesStat ? 5 : 0, Children = { Muted(values[index].Label, 10, semiBold: isSalesStat), value } }, new Thickness(20));
            grid.Children.Add(At(card, column: index));
        }
        return grid;
    }

    private static Border SmallHeader(string columns, string first, string second, string third, string fourth, Thickness? padding = null) =>
        SmallHeader(columns, [first, second, third, fourth], padding, new Thickness(-6, 0));

    private static Border SmallHeader(string columns, string first, string second, string third, string fourth, string fifth, string sixth, string seventh, Thickness? padding = null) =>
        SmallHeader(columns, [first, second, third, fourth, fifth, sixth, seventh], padding);

    private static Border SmallHeader(string columns, string first, string second, string third, string fourth, string fifth, string sixth, Thickness? padding = null) =>
        SmallHeader(columns, [first, second, third, fourth, fifth, sixth], padding);

    private static Border SmallHeader(string columns, string[] labels, Thickness? padding = null, Thickness? margin = null)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = labels.Length > 4 ? 12 : 0 };
        for (var index = 0; index < labels.Length; index++)
        {
            var text = new TextBlock { Text = labels[index], FontSize = 10, FontWeight = FontWeight.SemiBold };
            if (index == labels.Length - 1 && labels.Length == 4) text.TextAlignment = TextAlignment.Right;
            grid.Children.Add(At(text, column: index));
        }
        return Resource(new Border { Margin = margin ?? new Thickness(0), Padding = padding ?? new Thickness(10, 8), Child = grid }, Border.BackgroundProperty, "Muted");
    }

    private static StackPanel PageStack() => new() { Spacing = 18, Margin = new Thickness(0, 16, 0, 0) };
    private static ScrollViewer Scroll(Control content) => new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = content };

    private static Button Button(object? content, string command, bool primary = false)
    {
        var button = new Button { Content = content };
        Bind(button, Avalonia.Controls.Button.CommandProperty, command);
        if (primary) button.Classes.Add("primary");
        return button;
    }

    private static TextBlock Heading(string text, string className)
    {
        var block = new TextBlock { Text = text };
        block.Classes.Add(className);
        return block;
    }

    private static TextBlock BoundText(string path, string? format = null)
    {
        var block = new TextBlock();
        block.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = format });
        return block;
    }

    private static TextBlock SemiBold(string path)
    {
        var block = BoundText(path); block.FontWeight = FontWeight.SemiBold; return block;
    }

    private static TextBlock Muted(string? text = null, double? fontSize = null, string? path = null, bool semiBold = false)
    {
        var block = path is null ? new TextBlock { Text = text } : BoundText(path);
        if (fontSize is not null) block.FontSize = fontSize.Value;
        if (semiBold) block.FontWeight = FontWeight.SemiBold;
        return Resource(block, TextBlock.ForegroundProperty, "MutedForeground");
    }

    private static TextBlock Ellipsis(string path, bool semiBold = false, bool verticalCenter = false)
    {
        var block = BoundText(path); block.TextTrimming = TextTrimming.CharacterEllipsis;
        if (semiBold) block.FontWeight = FontWeight.SemiBold;
        if (verticalCenter) block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private static TextBlock Cell(string path, bool bold = false, TextAlignment alignment = TextAlignment.Left)
    {
        var block = BoundText(path); block.VerticalAlignment = VerticalAlignment.Center; block.TextAlignment = alignment;
        if (bold) block.FontWeight = FontWeight.Bold;
        return block;
    }

    private static Border RowBorder(Control child, Thickness padding, Thickness? thickness = null) =>
        Resource(new Border { BorderThickness = thickness ?? new Thickness(0, 0, 0, 1), Padding = padding, Child = child }, Border.BorderBrushProperty, "Border");

    private static Border Card(Control child, Thickness? padding = null, VerticalAlignment verticalAlignment = VerticalAlignment.Stretch, bool clip = false, Thickness? margin = null)
    {
        var card = new Border { Child = child, Padding = padding ?? new Thickness(0), VerticalAlignment = verticalAlignment, ClipToBounds = clip, Margin = margin ?? new Thickness(0) };
        card.Classes.Add("theme-card");
        return Resource(card, Border.BackgroundProperty, "Card");
    }

    private static T At<T>(T control, int column = 0, int row = 0) where T : Control
    {
        Grid.SetColumn(control, column); Grid.SetRow(control, row); return control;
    }

    private static T Bind<T>(T control, AvaloniaProperty property, string path) where T : AvaloniaObject
    {
        control.Bind(property, new Binding(path)); return control;
    }

    private static T Resource<T>(T control, AvaloniaProperty property, string key) where T : AvaloniaObject
    {
        control.Bind(property, new DynamicResourceExtension(key).ProvideValue(null!)); return control;
    }
}
