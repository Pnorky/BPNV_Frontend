using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;
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
        var status = Muted(path: "StatusMessage", fontSize: 11);
        var exportStatus = Muted(path: "ExportStatus", fontSize: 11);
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
        var dateRange = new DateRangePicker { PlaceholderText = "Pick a date range" };
        Bind(dateRange, DateRangePicker.StartDateProperty, "FromDate");
        Bind(dateRange, DateRangePicker.EndDateProperty, "ToDate");
        var applyDateRange = Button("Apply filters", "RefreshCommand", true);
        applyDateRange.VerticalAlignment = VerticalAlignment.Bottom;
        var customerType = new SelectDropdown();
        Bind(customerType, SelectDropdown.ItemsSourceProperty, "CustomerTypeOptions");
        Bind(customerType, SelectDropdown.SelectedItemProperty, "SelectedCustomerType");
        actions.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumnSpan(actions, 2);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("1.4*,0.85*,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 12,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, Muted("Sales, inventory, and supplier ordering summaries"), status, exportStatus } },
                At(actions, column: 1),
                At(Field("DATE RANGE", dateRange), row: 1),
                At(Field("SALES TYPE", customerType), column: 1, row: 1),
                At(applyDateRange, column: 2, row: 1)
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
            new TabItem { Header = "Inventory Summary", Content = BuildInventory() },
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
        topProducts.ItemTemplate = new FuncDataTemplate<TopProductResponse>((_, _) =>
        {
            var quantity = BoundText("Quantity", "{0} sold");
            Resource(quantity, TextBlock.ForegroundProperty, "MutedForeground");
            var sales = BoundText("SalesDisplay"); sales.FontWeight = FontWeight.Bold; sales.Width = 90; sales.TextAlignment = TextAlignment.Right;
            return RowBorder(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                ColumnSpacing = 20,
                Children = { Ellipsis("ProductName", true), At(quantity, column: 1), At(sales, column: 2) }
            }, new Thickness(0, 13), new Thickness(0, 1, 0, 0));
        }, true);

        var recentSales = new ItemsControl();
        Bind(recentSales, ItemsControl.ItemsSourceProperty, "RecentSales");
        recentSales.ItemTemplate = new FuncDataTemplate<ReportSaleResponse>((_, _) => RowBorder(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.15*,0.9*,0.45*,0.75*"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel { Children = { Ellipsis("SaleNumber", true), Muted(path: "TimeDisplay", fontSize: 10) } },
                At(Cell("CustomerType"), column: 1), At(Cell("ItemCount"), column: 2), At(Cell("TotalDisplay", true, TextAlignment.Right), column: 3)
            }
        }, new Thickness(4, 11)), true);

        var lower = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("0.9*,1.1*"),
            ColumnSpacing = 18,
            Children =
            {
                Card(new StackPanel { Spacing = 15, Children = { Heading("Top-selling products", "h2"), topProducts } }, new Thickness(22), VerticalAlignment.Top),
                At(Card(new StackPanel
                {
                    Spacing = 15,
                    Children = { Heading("Recent sales", "h2"), SmallHeader("1.15*,0.9*,0.45*,0.75*", "SALE", "PRICE TYPE", "ITEMS", "TOTAL"), recentSales }
                }, new Thickness(22), VerticalAlignment.Top), column: 1)
            }
        };
        content.Children.Add(lower);
        return content;
    }

    private static Control BuildInventory()
    {
        var content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 18,
            Margin = new Thickness(10, 28, 10, 12)
        };
        content.Children.Add(Stats(4,
            ("TOTAL UNITS", "TotalInventoryUnits"), ("DISPLAY", "DisplayUnits"),
            ("BODEGA", "BodegaUnits"), ("SELLING VALUE", "InventoryValueDisplay")));

        var summary = At(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 }, column: 1);
        summary.Children.Add(BoundText("MerchandiseCount", "{0} merchandise"));
        summary.Children.Add(BoundText("ConsumableCount", "{0} consumables"));
        summary.Children.Add(BoundText("SupplyCount", "{0} supplies"));
        var low = BoundText("LowStockItems", "{0} low stock"); low.FontWeight = FontWeight.SemiBold; summary.Children.Add(low);

        var table = new PagedTable
        {
            ItemName = "product",
            ItemNamePlural = "products",
            PageSize = 10,
            MinHeight = 0,
            MinTableWidth = 1050,
            IsSelectable = false
        };
        Bind(table, PagedTable.ItemsSourceProperty, "InventoryItems");
        var product = PagedTableColumn.Create<InventoryReportProductResponse, string>("PRODUCT", item => item.Name, new GridLength(1.6, GridUnitType.Star));
        product.CellTemplate = new FuncDataTemplate<InventoryReportProductResponse>((_, _) =>
            new StackPanel { Children = { SemiBold("Name"), Muted(path: "Sku", fontSize: 10) } }, true);
        var statusColumn = PagedTableColumn.Create<InventoryReportProductResponse, string>("STATUS", item => item.StockStatus, new GridLength(0.9, GridUnitType.Star));
        statusColumn.CellTemplate = new FuncDataTemplate<InventoryReportProductResponse>((_, _) => Badge("StockStatus"), true);
        table.Columns.Add(product);
        table.Columns.Add(PagedTableColumn.Create<InventoryReportProductResponse, string>("SUPPLIER", item => item.SupplierName, new GridLength(1.1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<InventoryReportProductResponse, string>("TYPE", item => item.ItemTypeDisplay, new GridLength(0.8, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<InventoryReportProductResponse, int>("DISPLAY", item => item.DisplayStock, new GridLength(0.6, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<InventoryReportProductResponse, int>("BODEGA", item => item.BodegaStock, new GridLength(0.6, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<InventoryReportProductResponse, int>("TOTAL", item => item.TotalStock, new GridLength(0.6, GridUnitType.Star)));
        table.Columns.Add(statusColumn);

        var tableContent = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(18, 16),
                    Children = { Heading("Inventory by location", "h2"), summary }
                },
                At(table, row: 1)
            }
        };
        content.Children.Add(At(tableContent, row: 1));
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
        summaries.ItemTemplate = new FuncDataTemplate<SupplierOrderResponse>((_, _) =>
        {
            var products = new ItemsControl();
            Bind(products, ItemsControl.ItemsSourceProperty, "Products");
            products.ItemTemplate = new FuncDataTemplate<OrderProductResponse>((_, _) =>
            {
                var order = BoundText("SuggestedOrderQuantity"); order.FontWeight = FontWeight.Bold; Resource(order, TextBlock.ForegroundProperty, "Primary");
                return RowBorder(new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("1.65*,1.1*,0.7*,0.7*,0.7*,0.7*,0.8*"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        SemiBold("ProductName"), At(BoundText("Sku"), column: 1), At(BoundText("TotalStock"), column: 2),
                        At(BoundText("ReorderTier"), column: 3), At(BoundText("CriticalReorderLevel"), column: 4),
                        At(BoundText("WarningReorderLevel"), column: 5), At(order, column: 6)
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
                    SmallHeader("1.65*,1.1*,0.7*,0.7*,0.7*,0.7*,0.8*", "PRODUCT", "SKU", "ON HAND", "TIER", "CRITICAL", "WARNING", "ORDER", new Thickness(12, 8)),
                    products
                }
            }, new Thickness(18), margin: new Thickness(8, 0, 8, 14));
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
    private static ScrollViewer Scroll(Control content) => new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(10, 4, 10, 12),
        Content = content
    };

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

    private static StackPanel Field(string label, Control control) =>
        new() { Spacing = 5, Children = { new TextBlock { Text = label, Classes = { "form-label" } }, control } };

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

    private static StatusBadge Badge(string path)
    {
        var badge = new StatusBadge();
        Bind(badge, StatusBadge.StatusProperty, path);
        return badge;
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
        control.Bind(property, new DynamicResourceExtension(key)); return control;
    }
}
