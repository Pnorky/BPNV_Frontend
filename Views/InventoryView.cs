using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class InventoryView : UserControl
{
    public InventoryView()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"),
            Margin = new Thickness(30),
            RowSpacing = 16
        };

        root.Children.Add(BuildHeader());
        root.Children.Add(At(BuildStatistics(), row: 1));
        root.Children.Add(At(BuildFilters(), row: 2));
        root.Children.Add(At(BuildSections(), row: 3));
        Content = root;
    }

    private static Control BuildHeader()
    {
        var title = BoundText("SectionTitle");
        title.Classes.Add("h1");
        var description = MutedText(path: "SectionDescription");
        var status = BoundText("StatusMessage");
        status.TextWrapping = TextWrapping.Wrap;
        status.FontSize = 12;

        var statusHost = Resource(new Border
        {
            CornerRadius = new CornerRadius(7),
            Padding = new Thickness(14, 8),
            MaxWidth = 430,
            VerticalAlignment = VerticalAlignment.Center,
            Child = status
        }, Border.BackgroundProperty, "Secondary");

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 16,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, description } },
                At(statusHost, column: 1)
            }
        };
    }

    private static Control BuildStatistics()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 14
        };
        Bind(grid, Visual.IsVisibleProperty, "IsProductsSection");
        grid.Children.Add(StatCard("ShelfUnits", "Units on display"));
        grid.Children.Add(At(StatCard("BodegaUnits", "Units in bodega"), column: 1));
        grid.Children.Add(At(StatCard("LowStockCount", "At reorder level"), column: 2));
        grid.Children.Add(At(StatCard("MissingReorderCount", "Reorder levels not set"), column: 3));
        return grid;
    }

    private static Border StatCard(string valuePath, string label)
    {
        var value = BoundText(valuePath);
        value.FontSize = 23;
        value.FontWeight = FontWeight.Bold;
        return Card(new StackPanel { Children = { value, MutedText(label) } }, new Thickness(16));
    }

    private static Control BuildFilters()
    {
        var search = new TextBox { PlaceholderText = "Search product, SKU, supplier, or category..." };
        search.Classes.Add("search");
        Bind(search, TextBox.TextProperty, "SearchText");

        var supplier = new SearchableSelect
        {
            PlaceholderText = "Filter by supplier",
            SearchTextSelector = item => item is SupplierItem value ? $"{value.Name} {value.ContactPerson} {value.Phone}" : item.ToString() ?? ""
        };
        Bind(supplier, SearchableSelect.ItemsSourceProperty, "Suppliers");
        Bind(supplier, SearchableSelect.SelectedItemProperty, "FilterSupplier");
        supplier.ItemTemplate = new FuncDataTemplate<SupplierItem>((_, _) => BoundText("Name"), true);

        var order = Button("Order summary", "ShowOrderSummaryCommand", true);
        var clear = Button("Clear filter", "ClearFilterCommand");
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,220,Auto,Auto"),
            ColumnSpacing = 10,
            Children = { search, At(supplier, column: 1), At(order, column: 2), At(clear, column: 3) }
        };
        Bind(grid, Visual.IsVisibleProperty, "IsProductsSection");
        return grid;
    }

    private static Control BuildSections()
    {
        var tabs = new TabControl
        {
            Items =
            {
                new TabItem { Header = "Products", Content = BuildProducts() },
                new TabItem { Header = "Suppliers", Content = BuildSuppliers() },
                new TabItem { Header = "Stock movements", Content = BuildMovements() }
            }
        };
        Bind(tabs, TabControl.SelectedIndexProperty, "SelectedSectionIndex", BindingMode.OneWay);
        tabs.Styles.Add(new Style(x => x.OfType<TabControl>().Template().OfType<ItemsPresenter>().Name("PART_ItemsPresenter"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, false) }
        });
        return tabs;
    }

    private static Control BuildProducts()
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            RowSpacing = 12,
            Margin = new Thickness(0, 14, 0, 0)
        };
        grid.Children.Add(Card(BuildProductExpander(), new Thickness(18)));

        var strip = new TabStrip { HorizontalAlignment = HorizontalAlignment.Left };
        Bind(strip, TabStrip.ItemsSourceProperty, "ProductTypes");
        Bind(strip, TabStrip.SelectedItemProperty, "SelectedProductType");
        grid.Children.Add(At(strip, row: 1));
        grid.Children.Add(At(BuildProductTable(), row: 2));
        return grid;
    }

    private static Control BuildProductExpander()
    {
        var expander = new Expander
        {
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = "Add a new product", FontWeight = FontWeight.SemiBold },
                    MutedText("Required product, price, and opening stock details", 11)
                }
            }
        };

        var supplier = new SearchableSelect
        {
            PlaceholderText = "Search supplier...",
            SearchTextSelector = item => item is SupplierItem value ? $"{value.Name} {value.Details}" : item.ToString() ?? ""
        };
        Bind(supplier, SearchableSelect.ItemsSourceProperty, "Suppliers");
        Bind(supplier, SearchableSelect.SelectedItemProperty, "NewProductSupplier");
        supplier.ItemTemplate = new FuncDataTemplate<SupplierItem>((_, _) =>
            new StackPanel
            {
                Margin = new Thickness(4, 3),
                Children = { SemiBold("Name"), MutedText(path: "Details", fontSize: 10) }
            }, true);

        var type = new SearchableSelect { PlaceholderText = "Select item type" };
        Bind(type, SearchableSelect.ItemsSourceProperty, "ItemTypes");
        Bind(type, SearchableSelect.SelectedItemProperty, "NewProductType");
        var name = new TextBox { PlaceholderText = "Required" };
        Bind(name, TextBox.TextProperty, "NewProductName");

        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.1*,1.5*,1.8*,0.65*,0.65*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 14,
            RowSpacing = 10,
            Margin = new Thickness(0, 16, 0, 0),
            Children =
            {
                Field("Supplier", supplier),
                At(Field("Item type", type), column: 1),
                At(Field("Product name", name), column: 2),
                At(Field("Critical level", Number("NewCriticalReorderLevel", "0")), column: 3),
                At(Field("Warning level", Number("NewWarningReorderLevel", "0", 1)), column: 4)
            }
        };

        var add = Button("Add product", "AddProductCommand", true);
        add.VerticalAlignment = VerticalAlignment.Bottom;
        add.Padding = new Thickness(20, 8);
        var prices = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*,Auto"),
            ColumnSpacing = 14,
            Children =
            {
                Field("Purchase price", Number("NewCostPrice", "0.00")),
                At(Field("Selling price", Number("NewRegularPrice", "0.00")), column: 1),
                At(Field("Employee price", Number("NewEmployeePrice", "0.00")), column: 2),
                At(Field("Critical order qty", Number("NewCriticalOrderQuantity", "0", 1)), column: 3),
                At(Field("Warning order qty", Number("NewWarningOrderQuantity", "0", 1)), column: 4),
                At(Field("Opening display", Number("NewOpeningShelf", "0")), column: 5),
                At(Field("Opening bodega", Number("NewOpeningBodega", "0")), column: 6),
                At(add, column: 7)
            }
        };
        Grid.SetRow(prices, 1);
        Grid.SetColumnSpan(prices, 5);
        form.Children.Add(prices);
        expander.Content = form;
        return expander;
    }

    private static Control BuildProductTable()
    {
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        Bind(list, ListBox.ItemsSourceProperty, "FilteredProducts");
        list.ItemTemplate = new FuncDataTemplate<ProductItem>((_, _) =>
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("1.8*,1.2*,0.65*,0.65*,0.65*,0.75*,1*"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Ellipsis("Name", true), MutedText(path: "Sku", fontSize: 10) } },
                    At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Ellipsis("SupplierName"), Ellipsis("ItemTypeDisplay", muted: true, fontSize: 10) } }, column: 1),
                    At(Cell("ShelfStock", true), column: 2), At(Cell("BodegaStock"), column: 3),
                    At(Cell("TotalStock", true, true), column: 4), At(Cell("WarningReorderDisplay"), column: 5),
                    At(Badge("StockStatus"), column: 6)
                }
            };
            return RowBorder(row, new Thickness(16, 10));
        }, true);

        return Card(new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { TableHeader("1.8*,1.2*,0.65*,0.65*,0.65*,0.75*,1*", "PRODUCT", "SUPPLIER", "DISPLAY", "BODEGA", "TOTAL", "WARNING", "STATUS"), At(list, row: 1) }
        }, clip: true);
    }

    private static Control BuildSuppliers()
    {
        var add = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Heading("Add supplier", "h3"),
                Label("Supplier name"), BoundTextBox("NewSupplierName", "Required"),
                Label("Contact person"), BoundTextBox("NewSupplierContact", "Optional"),
                Label("Phone"), BoundTextBox("NewSupplierPhone", "Optional")
            }
        };
        var addButton = Button("Add supplier", "AddSupplierCommand", true);
        addButton.HorizontalAlignment = HorizontalAlignment.Left;
        add.Children.Add(addButton);

        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        Bind(list, ListBox.ItemsSourceProperty, "Suppliers");
        list.ItemTemplate = new FuncDataTemplate<SupplierItem>((_, _) =>
        {
            var details = MutedText(path: "Details");
            return RowBorder(new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { SemiBold("Name"), At(details, column: 1) }
            }, new Thickness(8, 12));
        }, true);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("360,*"),
            ColumnSpacing = 14,
            Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                Card(add, new Thickness(18), verticalAlignment: VerticalAlignment.Top),
                At(Card(new StackPanel { Spacing = 12, Children = { Heading("Suppliers", "h3"), list } }, new Thickness(18)), column: 1)
            }
        };
        return grid;
    }

    private static Control BuildMovements()
    {
        var product = new SearchableSelect
        {
            PlaceholderText = "Search name, SKU, or supplier...",
            SearchTextSelector = item => item is ProductItem value ? value.MovementSelectorText : item.ToString() ?? ""
        };
        Bind(product, SearchableSelect.ItemsSourceProperty, "Products");
        Bind(product, SearchableSelect.SelectedItemProperty, "MovementProduct");
        product.ItemTemplate = new FuncDataTemplate<ProductItem>((_, _) =>
        {
            var total = BoundText("TotalDisplay"); total.FontWeight = FontWeight.SemiBold; total.HorizontalAlignment = HorizontalAlignment.Right;
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                ColumnSpacing = 16,
                Margin = new Thickness(4),
                Children =
                {
                    new StackPanel { Children = { SemiBold("Name"), MutedText(path: "MovementSelectorDetails", fontSize: 10) } },
                    At(new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { MutedText("Total", 10), total } }, column: 1)
                }
            };
        }, true);

        var movement = new SearchableSelect
        {
            PlaceholderText = "Search movement..."
        };
        Bind(movement, SearchableSelect.ItemsSourceProperty, "MovementOptions");
        Bind(movement, SearchableSelect.SelectedItemProperty, "SelectedMovement");
        var notes = BoundTextBox("MovementNotes", "Delivery receipt, reason, or note");
        var record = Button("Record movement", "ApplyMovementCommand", true);
        record.VerticalAlignment = VerticalAlignment.Bottom;

        var form = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.5*,1.2*,0.6*,1.5*,Auto"),
            ColumnSpacing = 10,
            Children =
            {
                Field("Product", product), At(Field("Movement", movement), column: 1),
                At(Field("Quantity", Number("MovementQuantity", "0", 1)), column: 2),
                At(Field("Reference / notes", notes), column: 3), At(record, column: 4)
            }
        };

        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        Bind(list, ListBox.ItemsSourceProperty, "RecentMovements");
        list.ItemTemplate = new FuncDataTemplate<StockMovement>((_, _) => RowBorder(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.5*,1*,1*,0.6*,1*,1.2*"),
            ColumnSpacing = 10,
            Children =
            {
                new StackPanel { Children = { SemiBold("ProductName"), MutedText(path: "Sku", fontSize: 10) } },
                At(Cell("SupplierName"), column: 1), At(Cell("TypeDisplay"), column: 2),
                At(Cell("QuantityDisplay", true, true), column: 3), At(Ellipsis("Notes", verticalCenter: true), column: 4),
                At(Cell("TimeDisplay"), column: 5)
            }
        }, new Thickness(16, 10)), true);

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 14, Margin = new Thickness(0, 14, 0, 0) };
        grid.Children.Add(Card(new StackPanel { Spacing = 12, Children = { Heading("Record stock movement", "h3"), form } }, new Thickness(18)));
        grid.Children.Add(At(Card(new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { TableHeader("1.5*,1*,1*,0.6*,1*,1.2*", "PRODUCT", "SUPPLIER", "MOVEMENT", "QTY", "NOTES", "DATE"), At(list, row: 1) }
        }, clip: true), row: 1));
        return grid;
    }

    private static Border TableHeader(string columns, params string[] labels)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(columns), ColumnSpacing = labels.Length == 7 ? 12 : 10 };
        for (var index = 0; index < labels.Length; index++)
        {
            var text = new TextBlock { Text = labels[index], FontSize = 11, FontWeight = FontWeight.SemiBold };
            grid.Children.Add(At(text, column: index));
        }
        return Resource(new Border { Padding = new Thickness(16, 10), Child = grid }, Border.BackgroundProperty, "Muted");
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 4, Children = { Label(label), control } };
    private static TextBlock Label(string text) => MutedText(text, 11);

    private static NumberField Number(string path, string format, decimal minimum = 0)
    {
        var number = new NumberField { Minimum = minimum, Increment = 1, FormatString = format };
        Bind(number, NumberField.ValueProperty, path);
        return number;
    }

    private static TextBox BoundTextBox(string path, string placeholder)
    {
        var box = new TextBox { PlaceholderText = placeholder };
        Bind(box, TextBox.TextProperty, path);
        return box;
    }

    private static Button Button(string content, string command, bool primary = false)
    {
        var button = new ActionButton(content, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary);
        Bind(button, Avalonia.Controls.Button.CommandProperty, command);
        return button;
    }

    private static TextBlock Heading(string text, string className)
    {
        var block = new TextBlock { Text = text };
        block.Classes.Add(className);
        return block;
    }

    private static TextBlock BoundText(string path)
    {
        var block = new TextBlock();
        Bind(block, TextBlock.TextProperty, path);
        return block;
    }

    private static TextBlock SemiBold(string path)
    {
        var block = BoundText(path);
        block.FontWeight = FontWeight.SemiBold;
        return block;
    }

    private static TextBlock MutedText(string? text = null, double? fontSize = null, string? path = null)
    {
        var block = path is null ? new TextBlock { Text = text } : BoundText(path);
        if (fontSize is not null) block.FontSize = fontSize.Value;
        return Resource(block, TextBlock.ForegroundProperty, "MutedForeground");
    }

    private static TextBlock Cell(string path, bool semiBold = false, bool bold = false)
    {
        var block = BoundText(path);
        block.VerticalAlignment = VerticalAlignment.Center;
        if (semiBold) block.FontWeight = FontWeight.SemiBold;
        if (bold) block.FontWeight = FontWeight.Bold;
        return block;
    }

    private static TextBlock MutedCell(string path)
    {
        var block = MutedText(path: path);
        block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private static StatusBadge Badge(string path)
    {
        var badge = new StatusBadge();
        Bind(badge, StatusBadge.StatusProperty, path);
        return badge;
    }

    private static TextBlock Ellipsis(string path, bool semiBold = false, bool muted = false, double? fontSize = null, bool verticalCenter = false)
    {
        var block = muted ? MutedText(path: path, fontSize: fontSize) : BoundText(path);
        if (!muted && fontSize is not null) block.FontSize = fontSize.Value;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        if (semiBold) block.FontWeight = FontWeight.SemiBold;
        if (verticalCenter) block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private static Border RowBorder(Control child, Thickness padding)
    {
        var border = Resource(new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = padding, Child = child }, Border.BorderBrushProperty, "Border");
        return border;
    }

    private static Border Card(Control child, Thickness? padding = null, bool clip = false, VerticalAlignment verticalAlignment = VerticalAlignment.Stretch)
    {
        var border = new Border { Child = child, Padding = padding ?? new Thickness(0), ClipToBounds = clip, VerticalAlignment = verticalAlignment };
        border.Classes.Add("theme-card");
        return Resource(border, Border.BackgroundProperty, "Card");
    }

    private static T At<T>(T control, int column = 0, int row = 0) where T : Control
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
        return control;
    }

    private static T Bind<T>(T control, AvaloniaProperty property, string path, BindingMode mode = BindingMode.Default) where T : AvaloniaObject
    {
        control.Bind(property, new Binding(path) { Mode = mode });
        return control;
    }

    private static T Resource<T>(T control, AvaloniaProperty property, string key) where T : AvaloniaObject
    {
        control.Bind(property, new DynamicResourceExtension(key));
        return control;
    }
}
