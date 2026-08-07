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

public class DashboardView : UserControl
{
    public DashboardView()
    {
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                Spacing = 22,
                Children =
                {
                    BuildHeader(),
                    BuildMetrics(),
                    BuildDetails()
                }
            }
        };
    }

    private static Grid BuildHeader()
    {
        var title = new TextBlock { Text = "Store overview" };
        title.Classes.Add("h1");

        var subtitle = new TextBlock
        {
            Text = "Sales performance and stock position across the display and bodega"
        };
        Resource(subtitle, TextBlock.ForegroundProperty, "MutedForeground");

        var refresh = new Button
        {
            Content = "Refresh",
            VerticalAlignment = VerticalAlignment.Center
        };
        refresh.Classes.Add("primary");
        refresh.Bind(Button.CommandProperty, new Binding("RefreshCommand"));
        Grid.SetColumn(refresh, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, subtitle } },
                refresh
            }
        };
    }

    private static Grid BuildMetrics()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 14
        };
        grid.Children.Add(BuildMetric(0, LucideIconKind.CircleDollarSign, "TodaySalesDisplay", "Sales today"));
        grid.Children.Add(BuildMetric(1, LucideIconKind.ShoppingBag, "TodayTransactions", "Transactions today"));
        grid.Children.Add(BuildMetric(2, LucideIconKind.Store, "ShelfUnits", "Units on display"));
        grid.Children.Add(BuildMetric(3, LucideIconKind.Warehouse, "BodegaUnits", "Units in bodega"));
        return grid;
    }

    private static Border BuildMetric(int column, LucideIconKind iconKind, string valuePath, string label)
    {
        var icon = new LucideIcon
        {
            Kind = iconKind,
            Width = 22,
            Height = 22,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        Resource(icon, LucideIcon.ForegroundProperty, "Primary");

        var value = new TextBlock { FontSize = 27, FontWeight = FontWeight.Bold };
        value.Bind(TextBlock.TextProperty, new Binding(valuePath));

        var caption = new TextBlock { Text = label };
        Resource(caption, TextBlock.ForegroundProperty, "MutedForeground");

        var card = Card(new StackPanel
        {
            Spacing = 8,
            Children = { icon, value, caption }
        }, new Thickness(20));
        Grid.SetColumn(card, column);
        return card;
    }

    private static Grid BuildDetails()
    {
        var attention = Card(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                BuildAttentionHeader(),
                BuildAttentionItems()
            }
        }, new Thickness(22));

        var recentTitle = new TextBlock { Text = "Recent sales" };
        recentTitle.Classes.Add("h2");
        var recent = Card(new StackPanel
        {
            Spacing = 14,
            Children = { recentTitle, BuildRecentSales() }
        }, new Thickness(22));
        recent.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(recent, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.1*,0.9*"),
            ColumnSpacing = 18,
            Children = { attention, recent }
        };
    }

    private static Grid BuildAttentionHeader()
    {
        var title = new TextBlock { Text = "Stock attention" };
        title.Classes.Add("h2");
        var subtitle = new TextBlock
        {
            Text = "Low total stock or empty display locations",
            FontSize = 12
        };
        Resource(subtitle, TextBlock.ForegroundProperty, "MutedForeground");

        var count = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.Parse("#7A5200")),
            FontWeight = FontWeight.SemiBold
        };
        count.Bind(TextBlock.TextProperty, new Binding("AttentionCount") { StringFormat = "{0} items" });
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FFF2CC")),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Child = count
        };
        Grid.SetColumn(badge, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Children = { title, subtitle } },
                badge
            }
        };
    }

    private static ItemsControl BuildAttentionItems()
    {
        var items = new ItemsControl();
        items.Bind(ItemsControl.ItemsSourceProperty, new Binding("AttentionItems"));
        items.ItemTemplate = new FuncDataTemplate<ProductItem>((_, _) =>
        {
            var name = BoundText("Name", FontWeight.SemiBold);
            var sku = BoundText("Sku", fontSize: 11, resource: "MutedForeground");
            var displayLabel = MutedLabel("Display");
            var display = BoundText("ShelfDisplay", FontWeight.SemiBold);
            var bodegaLabel = MutedLabel("Bodega");
            var bodega = BoundText("BodegaDisplay", FontWeight.SemiBold);

            var displayStack = new StackPanel { Children = { displayLabel, display } };
            Grid.SetColumn(displayStack, 1);
            var bodegaStack = new StackPanel { Width = 90, Children = { bodegaLabel, bodega } };
            Grid.SetColumn(bodegaStack, 2);

            var border = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 12),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
                    ColumnSpacing = 18,
                    Children =
                    {
                        new StackPanel { Children = { name, sku } },
                        displayStack,
                        bodegaStack
                    }
                }
            };
            Resource(border, Border.BorderBrushProperty, "Border");
            return border;
        }, true);
        return items;
    }

    private static ItemsControl BuildRecentSales()
    {
        var items = new ItemsControl();
        items.Bind(ItemsControl.ItemsSourceProperty, new Binding("RecentSales"));
        items.ItemTemplate = new FuncDataTemplate<SaleRecord>((_, _) =>
        {
            var number = BoundText("SaleNumber", FontWeight.SemiBold);
            var customer = BoundText("CustomerType", fontSize: 11, resource: "MutedForeground", stringFormat: "{0} price");
            var total = BoundText("TotalDisplay", FontWeight.Bold);
            total.HorizontalAlignment = HorizontalAlignment.Right;
            var time = BoundText("TimeDisplay", fontSize: 11, resource: "MutedForeground");
            time.HorizontalAlignment = HorizontalAlignment.Right;

            var values = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { total, time }
            };
            Grid.SetColumn(values, 1);

            var border = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(0, 13),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel { Children = { number, customer } },
                        values
                    }
                }
            };
            Resource(border, Border.BorderBrushProperty, "Border");
            return border;
        }, true);
        return items;
    }

    private static Border Card(Control child, Thickness padding)
    {
        var card = new Border { Padding = padding, Child = child };
        card.Classes.Add("theme-card");
        Resource(card, Border.BackgroundProperty, "Card");
        return card;
    }

    private static TextBlock MutedLabel(string text)
    {
        var label = new TextBlock { Text = text, FontSize = 10 };
        Resource(label, TextBlock.ForegroundProperty, "MutedForeground");
        return label;
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
