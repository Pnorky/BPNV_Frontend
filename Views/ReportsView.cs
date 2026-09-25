using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

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
            Children = { BuildStatus(), At(BuildTabs(), row: 1) }
        };
    }

    private static Control BuildStatus()
    {
        var status = Muted(path: "StatusMessage", fontSize: 11);
        var exportStatus = Muted(path: "ExportStatus", fontSize: 11);
        var panel = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(7),
            Child = new StackPanel { Spacing = 2, Children = { status, exportStatus } }
        };
        panel.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));
        return panel;
    }

    private static Control BuildTabs()
    {
        var sales = new TabItem { Header = "Sales Summary", Content = Scroll(BuildSales()) };
        var employee = new TabItem { Header = "Employee Purchases", Content = BuildEmployeePurchases() };
        var remittance = new TabItem { Header = "Cashier Remittance", Content = BuildCashierRemittance() };
        var inventory = new TabItem { Header = "Inventory Summary", Content = BuildInventory() };
        var orders = new TabItem { Header = "Order Summary", Content = Scroll(BuildOrders()) };
        var accountability = new TabItem { Header = "Sales Accountability", Content = BuildSalesAccountability() };
        foreach (var tab in new[] { sales, employee, remittance, inventory, orders, accountability })
            tab.FontSize = 18;
        foreach (var tab in new[] { sales, employee, remittance, inventory, orders })
            tab.Bind(Visual.IsVisibleProperty, new Binding("CanViewStandardReports"));

        var strip = new TabStrip
        {
            Items = { sales, employee, remittance, inventory, orders, accountability },
            HorizontalAlignment = HorizontalAlignment.Left
        };
        strip.Bind(TabStrip.SelectedIndexProperty, new Binding("SelectedReportTabIndex") { Mode = BindingMode.TwoWay });
        strip.Bind(Visual.IsVisibleProperty, new Binding("CanViewStandardReports"));

        var content = new Grid();
        AddReportContent(content, sales.Content, "IsSalesReportTab");
        AddReportContent(content, employee.Content, "IsEmployeePurchasesTab");
        AddReportContent(content, remittance.Content, "IsCashierRemittanceTab");
        AddReportContent(content, inventory.Content, "IsInventoryReportTab");
        AddReportContent(content, orders.Content, "IsOrderReportTab");
        AddReportContent(content, accountability.Content, "IsSalesAccountabilityTab");

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 8,
            Children =
            {
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = strip
                },
                At(content, row: 1)
            }
        };
    }

    private static void AddReportContent(Grid host, object? value, string visibilityPath)
    {
        if (value is not Control control) return;
        Bind(control, Visual.IsVisibleProperty, visibilityPath);
        host.Children.Add(control);
    }

    private static Control BuildSalesAccountability()
    {
        var dateHeaders = new ItemsControl();
        Bind(dateHeaders, ItemsControl.ItemsSourceProperty, "SalesAccountability.Dates");
        dateHeaders.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal });
        dateHeaders.ItemTemplate = new FuncDataTemplate<DateOnly>((_, _) =>
        {
            var date = new TextBlock { FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
            date.Bind(TextBlock.TextProperty, new Binding(".") { StringFormat = "{0:MMM d}" });
            return new StackPanel
            {
                Width = 240,
                Spacing = 6,
                Children =
                {
                    date,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        Children =
                        {
                            At(ColumnHeader("REGULAR SALES"), column: 0),
                            At(ColumnHeader("EMPLOYEE SALES"), column: 1)
                        }
                    }
                }
            };
        }, true);

        var rows = new ItemsControl();
        Bind(rows, ItemsControl.ItemsSourceProperty, "AccountabilityRows");
        rows.ItemTemplate = new FuncDataTemplate<SalesAccountabilityCategoryRowViewModel>((_, _) =>
        {
            var values = new ItemsControl();
            Bind(values, ItemsControl.ItemsSourceProperty, nameof(SalesAccountabilityCategoryRowViewModel.Values));
            values.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal });
            values.ItemTemplate = new FuncDataTemplate<SalesAccountabilityValueViewModel>((_, _) => new Grid
            {
                Width = 240,
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                Children =
                {
                    MoneyText(nameof(SalesAccountabilityValueViewModel.RegularDisplay)),
                    At(MoneyText(nameof(SalesAccountabilityValueViewModel.EmployeeDisplay)), column: 1)
                }
            }, true);
            return RowBorder(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("140,Auto"),
                Children = { SemiBold(nameof(SalesAccountabilityCategoryRowViewModel.Category)), At(values, column: 1) }
            }, new Thickness(8, 10));
        }, true);

        var matrix = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Children =
                {
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("140,Auto"),
                         Children =
                         {
                             new TextBlock
                             {
                                 Text = "CATEGORY", FontWeight = FontWeight.SemiBold,
                                 Margin = new Thickness(8, 0)
                             },
                             At(dateHeaders, column: 1)
                         }
                    },
                    rows
                }
            }
        };

        var note = Muted("Products assigned to Other are included in the Other row. Edit those products to classify them.");
        note.Bind(Visual.IsVisibleProperty, new Binding("HasOtherReportCategories"));

        var daily = new PagedTable
        {
            ItemName = "business date", ItemNamePlural = "business dates", PageSize = 12,
            MinHeight = 300, MinTableWidth = 1350, IsSelectable = false
        };
        Bind(daily, PagedTable.ItemsSourceProperty, "SalesAccountability.Days");
        daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("DATE", item => item.DateDisplay, new GridLength(1, GridUnitType.Star)));
        daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("TOTAL SALES", item => Money(item.TotalSales), new GridLength(0.9, GridUnitType.Star)));
        daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("CASH", item => Money(item.CashSales), new GridLength(0.8, GridUnitType.Star)));
         daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("GCASH PAYMENTS", item => Money(item.GCashPayments), new GridLength(0.8, GridUnitType.Star)));
        daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("EXPENSES", item => Money(item.ApprovedExpenses), new GridLength(0.8, GridUnitType.Star)));
         daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("CASH REMITTED", item => Money(item.CashRemitted), new GridLength(0.9, GridUnitType.Star)));
        daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("EXPECTED", item => Money(item.ExpectedCash), new GridLength(0.9, GridUnitType.Star)));
         daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("CASH DIFFERENCE", item => Money(item.Variance), new GridLength(0.9, GridUnitType.Star)));
         daily.Columns.Add(PagedTableColumn.Create<SalesAccountabilityDayResponse, string>("ASSIGNED CASHIERS", item => item.CashiersDisplay, new GridLength(1.4, GridUnitType.Star)));
        var cashAccountability = Card(new StackPanel { Spacing = 12, Children = { Heading("Daily cash accountability", "h2"), daily } }, new Thickness(18));
        Bind(cashAccountability, Visual.IsVisibleProperty, "SalesAccountability.IncludesCashAccountability");

        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 18,
                Margin = new Thickness(10, 28, 10, 12),
                Children =
                {
                    Card(new StackPanel { Spacing = 12, Children = { Heading("Regular and employee sales by category", "h2"), note, matrix } }, new Thickness(18)),
                    cashAccountability
                }
            }
        };
    }

    private static TextBlock Centered(string text, double fontSize = 11) => new()
    {
        Text = text, FontSize = fontSize, FontWeight = FontWeight.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center
    };

    private static TextBlock ColumnHeader(string text) => new()
    {
        Text = text,
        FontSize = 10,
        FontWeight = FontWeight.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        TextAlignment = TextAlignment.Right,
        Margin = new Thickness(0, 0, 8, 0)
    };

    private static TextBlock MoneyText(string path)
    {
        var text = BoundText(path);
        text.TextAlignment = TextAlignment.Right;
        text.Margin = new Thickness(8, 0);
        return text;
    }

    private static string Money(decimal? value) => value.HasValue ? $"₱{value:N2}" : "-";

    private static Control BuildEmployeePurchases()
    {
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 18, Margin = new Thickness(10, 28, 10, 12) };
        content.Children.Add(Stats(4,
            ("TOTAL DEDUCTIONS", "EmployeeDeductionsDisplay"),
            ("AMOUNT OWED", "EmployeeOwedDisplay"),
            ("EMPLOYEE SALES", "EmployeeTransactions"),
            ("EMPLOYEES", "EmployeesRepresented")));
        var table = new PagedTable { ItemName = "purchase line", ItemNamePlural = "purchase lines", PageSize = 12, MinHeight = 0, MinTableWidth = 1450, IsSelectable = false };
        Bind(table, PagedTable.ItemsSourceProperty, "EmployeePurchaseLines");
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("DATE & TIME", item => item.SoldAtDisplay, new GridLength(220)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("SALE", item => item.SaleNumber, new GridLength(170)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("EMPLOYEE", item => item.EmployeeDisplay, new GridLength(260)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("SKU", item => item.Sku, new GridLength(170)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("PRODUCT", item => item.ProductName, new GridLength(220)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("QUANTITY", item => item.QuantityDisplay, new GridLength(220)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("UNIT PRICE", item => item.UnitPriceDisplay, new GridLength(150)));
        table.Columns.Add(PagedTableColumn.Create<EmployeePurchaseLineResponse, string>("LINE TOTAL", item => item.LineTotalDisplay, new GridLength(150)));
        content.Children.Add(At(table, row: 1));
        return content;
    }

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
            ColumnDefinitions = new ColumnDefinitions("1.15*,0.8*,0.75*,0.45*,0.75*"),
            ColumnSpacing = 12,
            Children =
            {
                new StackPanel { Children = { Ellipsis("SaleNumber", true), Muted(path: "TimeDisplay", fontSize: 10) } },
                At(Cell("CustomerType"), column: 1), At(Cell("PaymentMethodDisplay"), column: 2),
                At(Cell("ItemCount"), column: 3), At(Cell("TotalDisplay", true, TextAlignment.Right), column: 4)
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
                    Children = { Heading("Recent sales", "h2"), SmallHeader("1.15*,0.8*,0.75*,0.45*,0.75*", ["SALE", "PRICE", "PAYMENT", "ITEMS", "TOTAL"]), recentSales }
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

    private static Control BuildCashierRemittance()
    {
        var content = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 14, Margin = new Thickness(10, 28, 10, 12) };
        content.Children.Add(Stats(4,
            ("SHIFT SESSIONS", "Snapshot.CashierShifts.Summary.Sessions"),
            ("CASH COLLECTED", "Snapshot.CashierShifts.Summary.CashSales"),
            ("EXPECTED CASH", "Snapshot.CashierShifts.Summary.ExpectedRemittance"),
            ("REMITTANCE DIFFERENCE", "Snapshot.CashierShifts.Summary.Variance")));

        var table = new PagedTable { ItemName = "cashier session", ItemNamePlural = "cashier sessions", PageSize = 12, MinHeight = 0, MinTableWidth = 2200, IsSelectable = false };
        Bind(table, PagedTable.ItemsSourceProperty, "Snapshot.CashierShifts.Sessions");
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("DATE", row => row.BusinessDate.ToString("MMMM d, yyyy"), new GridLength(2.0, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CASHIER", row => row.CashierName, new GridLength(1.3, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("SHIFT", row => row.ShiftName, new GridLength(1.1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("STATUS", row => row.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance ? "Pending Remittance" : row.Status.ToString(), new GridLength(1.2, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("SALES", row => CashierShiftFormatting.Money(row.TotalSales), new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("EXPECTED", row => CashierShiftFormatting.Money(row.ExpectedRemittance), new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("ACTUAL", row => CashierShiftFormatting.Money(row.ActualRemittance), new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("REMITTANCE DIFFERENCE", row => CashierShiftFormatting.SignedMoney(row.Variance), new GridLength(1.3, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("STARTING CASH", row => CashierShiftFormatting.Money(row.OpeningCashFloat), new GridLength(1.1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CASH REFUNDS", row => CashierShiftFormatting.Money(row.CashRefunds), new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CASH PAYOUTS", row => CashierShiftFormatting.Money(row.CashPayouts), new GridLength(1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("SALES COUNT", row => (row.TransactionCount ?? 0).ToString(), new GridLength(0.9, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CLOCK IN", row => StoreDateTime.ToStoreTimeFromUtc(row.ClockedInAtUtc).ToString("h:mm tt"), new GridLength(0.9, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CLOCK OUT", row => row.ClockedOutAtUtc is { } value ? StoreDateTime.ToStoreTimeFromUtc(value).ToString("h:mm tt") : "-", new GridLength(0.9, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("SHIFT LENGTH", row => row.WorkedMinutes is { } minutes ? $"{minutes / 60}h {minutes % 60}m" : "-", new GridLength(1.1, GridUnitType.Star)));
        table.Columns.Add(PagedTableColumn.Create<CashierShiftReportRowResponse, string>("CASH RETURNED", row => row.CashFloatReturned == true ? "Yes" : "No", new GridLength(1.2, GridUnitType.Star)));
        content.Children.Add(At(table, row: 1));
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
                        SemiBold("ProductName"), At(BoundText("Sku"), column: 1), At(BoundText("BodegaStock"), column: 2),
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

    private static TextBlock Heading(string text, string className)
    {
        var block = new TextBlock { Text = text };
        block.Classes.Add(className);
        return block;
    }

    private static TextBlock BoundText(string path, string? format = null)
    {
        var block = new TextBlock { FontSize = 14 };
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
