using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace AvaloniaApp.Views;

public class SalesView : UserControl
{
    public SalesView()
    {
        var body = BuildBody();
        Grid.SetRow(body, 1);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(30),
            RowSpacing = 20,
            Children = { BuildHeader(), body }
        };
    }

    private static Grid BuildHeader()
    {
        var title = new TextBlock { Text = "New sale" };
        title.Classes.Add("h1");
        var subtitle = new TextBlock
        {
            Text = "A simple sales entry workflow without register or receipt handling"
        };
        Resource(subtitle, TextBlock.ForegroundProperty, "MutedForeground");

        var pricingLabel = new TextBlock
        {
            Text = "Pricing",
            VerticalAlignment = VerticalAlignment.Center
        };
        Resource(pricingLabel, TextBlock.ForegroundProperty, "MutedForeground");
        var pricing = new ComboBox { MinWidth = 145 };
        pricing.Bind(ItemsControl.ItemsSourceProperty, new Binding("CustomerTypes"));
        pricing.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedCustomerType"));

        var pricingPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { pricingLabel, pricing }
        };
        Grid.SetColumn(pricingPanel, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 20,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, subtitle } },
                pricingPanel
            }
        };
    }

    private static Grid BuildBody()
    {
        var catalog = Card(BuildCatalog(), new Thickness(20), new Thickness(0, 0, 14, 0));

        var splitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns
        };
        Resource(splitter, Panel.BackgroundProperty, "Border");
        Grid.SetColumn(splitter, 1);

        var sale = Card(BuildCurrentSale(), new Thickness(20), new Thickness(14, 0, 0, 0));
        Grid.SetColumn(sale, 2);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,5,390"),
            ColumnSpacing = 0,
            Children = { catalog, splitter, sale }
        };
    }

    private static Grid BuildCatalog()
    {
        var search = new TextBox
        {
            PlaceholderText = "Search product, SKU, supplier, or category..."
        };
        search.Classes.Add("search");
        search.Bind(TextBox.TextProperty, new Binding("SearchText"));

        var products = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        products.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredProducts"));
        products.ItemTemplate = new FuncDataTemplate<ProductItem>((_, _) => BuildProductRow(), true);
        Grid.SetRow(products, 1);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14,
            Children = { search, products }
        };
    }

    private static Border BuildProductRow()
    {
        var name = BoundText("Name", FontWeight.SemiBold, 14);
        var details = BoundText("CatalogDetails", fontSize: 11, resource: "MutedForeground");
        details.TextWrapping = TextWrapping.Wrap;
        details.MaxLines = 2;

        var regular = PriceColumn(1, "Regular", "RegularPriceDisplay");
        var employee = PriceColumn(2, "Employee", "EmployeePriceDisplay");
        var add = new Button
        {
            Content = "Add",
            VerticalAlignment = VerticalAlignment.Center
        };
        add.Classes.Add("primary");
        add.Bind(Button.CommandProperty, AncestorCommand("AddProductCommand"));
        add.Bind(Button.CommandParameterProperty, new Binding());
        Grid.SetColumn(add, 3);

        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 14),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,88,88,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Margin = new Thickness(0, 0, 8, 0),
                        Children = { name, details }
                    },
                    regular,
                    employee,
                    add
                }
            }
        };
        Resource(border, Border.BorderBrushProperty, "Border");
        return border;
    }

    private static StackPanel PriceColumn(int column, string label, string path)
    {
        var caption = new TextBlock { Text = label, FontSize = 10 };
        Resource(caption, TextBlock.ForegroundProperty, "MutedForeground");
        var value = BoundText(path, FontWeight.Bold);
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { caption, value }
        };
        Grid.SetColumn(stack, column);
        return stack;
    }

    private static Grid BuildCurrentSale()
    {
        var heading = BuildSaleHeading();
        var cart = BuildCart();
        Grid.SetRow(cart, 1);
        var footer = BuildSaleFooter();
        Grid.SetRow(footer, 2);

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            RowSpacing = 16,
            Children = { heading, cart, footer }
        };
    }

    private static Grid BuildSaleHeading()
    {
        var title = new TextBlock { Text = "Current sale" };
        title.Classes.Add("h2");
        var summary = BoundText("CartSummary", fontSize: 12, resource: "MutedForeground");
        var clear = new Button
        {
            Content = "Clear",
            Background = Brushes.Transparent
        };
        Resource(clear, Button.BorderBrushProperty, "Border");
        clear.Bind(Button.CommandProperty, new Binding("ClearSaleCommand"));
        Grid.SetColumn(clear, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Children = { title, summary } },
                clear
            }
        };
    }

    private static ListBox BuildCart()
    {
        var cart = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        cart.Bind(ItemsControl.ItemsSourceProperty, new Binding("Cart"));
        cart.ItemTemplate = new FuncDataTemplate<CartLine>((_, _) => BuildCartLine(), true);
        return cart;
    }

    private static Border BuildCartLine()
    {
        var product = BoundText("Product.Name", FontWeight.SemiBold);
        product.TextTrimming = TextTrimming.CharacterEllipsis;
        product.VerticalAlignment = VerticalAlignment.Center;

        var remove = IconButton(BuildRemoveIcon());
        remove.Background = Brushes.Transparent;
        remove.BorderThickness = new Thickness(0);
        remove.Bind(Button.CommandProperty, AncestorCommand("RemoveLineCommand"));
        remove.Bind(Button.CommandParameterProperty, new Binding());
        Grid.SetColumn(remove, 1);

        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { product, remove }
        };

        var decrease = QuantityButton(BuildMinusIcon(), "DecreaseQuantityCommand");
        var quantity = BoundText("Quantity", FontWeight.Bold);
        quantity.TextAlignment = TextAlignment.Center;
        quantity.HorizontalAlignment = HorizontalAlignment.Stretch;
        quantity.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(quantity, 1);
        var increase = QuantityButton(BuildPlusIcon(), "IncreaseQuantityCommand");
        Grid.SetColumn(increase, 2);
        var unitPrice = BoundText("UnitPriceDisplay", resource: "MutedForeground", stringFormat: "@ {0}");
        unitPrice.Margin = new Thickness(4, 0, 0, 0);
        unitPrice.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(unitPrice, 3);
        var amount = BoundText("AmountDisplay", FontWeight.Bold);
        amount.VerticalAlignment = VerticalAlignment.Center;
        amount.HorizontalAlignment = HorizontalAlignment.Stretch;
        amount.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(amount, 4);

        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 12),
            Child = new StackPanel
            {
                Spacing = 9,
                Children =
                {
                    heading,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("36,36,36,*,90"),
                        ColumnSpacing = 8,
                        Children = { decrease, quantity, increase, unitPrice, amount }
                    }
                }
            }
        };
        Resource(border, Border.BorderBrushProperty, "Border");
        return border;
    }

    private static Button QuantityButton(Canvas icon, string commandPath)
    {
        var button = IconButton(icon);
        button.Width = 36;
        button.Height = 36;
        button.Bind(Button.CommandProperty, AncestorCommand(commandPath));
        button.Bind(Button.CommandParameterProperty, new Binding());
        return button;
    }

    private static Button IconButton(Canvas icon) => new()
    {
        Width = 28,
        Height = 28,
        Padding = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
        Content = icon
    };

    private static Canvas BuildRemoveIcon() => IconCanvas("M 3,3 L 15,15 M 15,3 L 3,15");

    private static Canvas BuildMinusIcon() => IconCanvas("M 2,9 L 16,9");

    private static Canvas BuildPlusIcon() => IconCanvas("M 2,9 L 16,9 M 9,2 L 9,16");

    private static Canvas IconCanvas(string data)
    {
        var path = new ShapePath
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round
        };
        Resource(path, Shape.StrokeProperty, "Foreground");
        return new Canvas { Width = 18, Height = 18, Children = { path } };
    }

    private static StackPanel BuildSaleFooter()
    {
        var total = BoundText("TotalDisplay", FontWeight.Bold, 24);
        Grid.SetColumn(total, 1);
        var totalPanel = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock { Text = "Total", FontSize = 16, FontWeight = FontWeight.SemiBold },
                    total
                }
            }
        };
        Resource(totalPanel, Border.BackgroundProperty, "Muted");

        var status = BoundText("StatusMessage", fontSize: 12, resource: "MutedForeground");
        status.TextWrapping = TextWrapping.Wrap;
        var complete = new Button
        {
            Content = "Complete sale",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(16, 12)
        };
        complete.Classes.Add("primary");
        complete.Bind(Button.CommandProperty, new Binding("CompleteSaleCommand"));

        return new StackPanel
        {
            Spacing = 12,
            Children = { totalPanel, status, complete }
        };
    }

    private static Binding AncestorCommand(string commandPath) => new($"DataContext.{commandPath}")
    {
        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
        {
            AncestorType = typeof(ListBox)
        }
    };

    private static Border Card(Control child, Thickness padding, Thickness margin)
    {
        var card = new Border { Padding = padding, Margin = margin, Child = child };
        card.Classes.Add("theme-card");
        Resource(card, Border.BackgroundProperty, "Card");
        return card;
    }

    private static TextBlock BoundText(
        string path,
        FontWeight? fontWeight = null,
        double? fontSize = null,
        string? resource = null,
        string? stringFormat = null)
    {
        var text = new TextBlock();
        if (fontWeight is { } weight)
            text.FontWeight = weight;
        if (fontSize is { } size)
            text.FontSize = size;
        if (resource is not null)
            Resource(text, TextBlock.ForegroundProperty, resource);
        text.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = stringFormat });
        return text;
    }

    private static void Resource(AvaloniaObject target, AvaloniaProperty property, object key) =>
        target.Bind(property, new DynamicResourceExtension(key));
}
