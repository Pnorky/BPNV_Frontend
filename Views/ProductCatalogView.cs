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

public class ProductCatalogView : UserControl
{
    public ProductCatalogView()
    {
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredProducts"));
        list.ItemTemplate = new FuncDataTemplate<ProductResponse>((_, _) => ProductRow(), true);
        Grid.SetRow(list, 1);
        var tableSurface = new Grid
        {
            MinWidth = 1420,
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { CatalogHeader(), list }
        };
        var card = new Border
        {
            Padding = new Thickness(0),
            ClipToBounds = true,
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = tableSurface
            }
        };
        card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));

        Content = new Grid
        {
            Margin = new Thickness(30),
            RowDefinitions = new RowDefinitions("Auto,*"),
            RowSpacing = 14,
            Children =
            {
                Status(), At(card, 1)
            }
        };
    }

    private static Control ProductRow()
    {
        var edit = Action("Edit", "DataContext.EditCommand");
        var stockCount = Action("Stock count", "DataContext.RecordStockCountCommand");
        stockCount.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProductResponse.CanRecordStockCount)));
        var labels = Action("Labels", "DataContext.LabelsCommand");
        labels.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProductResponse.IsActive)));
        var deactivate = DangerLink("Deactivate", "DataContext.DeactivateCommand");
        deactivate.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProductResponse.IsActive)));
        var reactivate = Action("Reactivate", "DataContext.ReactivateCommand");
        reactivate.Bind(Visual.IsVisibleProperty, new Binding(nameof(ProductResponse.IsInactive)));
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { edit, labels, stockCount, deactivate, reactivate }
        };
        var row = new Grid
        {
            MinWidth = 1550,
            ColumnDefinitions = CatalogColumns(),
            ColumnSpacing = 14,
            Children =
            {
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text("Name", FontWeight.SemiBold), Text("Sku", resource: "MutedForeground"), Text("BarcodeDisplay", resource: "MutedForeground") } },
                At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text("SupplierName"), Text("Category", resource: "MutedForeground") } }, column: 1),
                At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 4, Children = { Text("ItemType"), Text("HandlingDisplay", resource: "MutedForeground"), Badge("StockStatus") } }, column: 2),
                At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text("StockDisplay"), Text("ReorderActionDisplay", resource: "MutedForeground") } }, column: 3),
                At(Badge(nameof(ProductResponse.ActivityStatus)), column: 4),
                At(Text("PurchasePriceDisplay", FontWeight.SemiBold, vertical: true), column: 5),
                At(Text("SellingPriceDisplay", FontWeight.SemiBold, vertical: true), column: 6),
                At(Text("EmployeePriceDisplay", FontWeight.SemiBold, vertical: true), column: 7),
                At(actions, column: 8)
            }
        };
        var border = new Border
        {
            MinHeight = 112,
            VerticalAlignment = VerticalAlignment.Stretch,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(16, 12),
            Child = row
        };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Border CatalogHeader()
    {
        var grid = new Grid { MinWidth = 1550, ColumnDefinitions = CatalogColumns(), ColumnSpacing = 14 };
        var labels = new[] { "PRODUCT", "SUPPLIER", "TYPE / STOCK", "STOCK / REORDER", "STATUS", "PURCHASE / PIECE", "SELLING", "EMPLOYEE", "ACTIONS" };
        for (var index = 0; index < labels.Length; index++)
        {
            var label = Muted(labels[index]);
            label.FontSize = 10;
            label.FontWeight = FontWeight.SemiBold;
            label.LetterSpacing = 0.6;
            if (index == labels.Length - 1)
                label.HorizontalAlignment = HorizontalAlignment.Right;
            grid.Children.Add(At(label, column: index));
        }

        var header = new Border { Padding = new Thickness(16, 10), Child = grid };
        header.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Muted"));
        return header;
    }

    private static ColumnDefinitions CatalogColumns() => new("220,165,170,180,120,120,100,110,330");

    private static Button Action(string text, string commandPath)
    {
        var button = new ActionButton(text, ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        button.Bind(Button.CommandProperty, new Binding(commandPath)
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ProductCatalogView) }
        });
        button.Bind(Button.CommandParameterProperty, new Binding());
        return button;
    }

    private static Border Status() { var value = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Text("StatusMessage") }; value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return value; }
    private static StatusBadge Badge(string path) { var value = new StatusBadge { VerticalAlignment = VerticalAlignment.Center }; value.Bind(StatusBadge.StatusProperty, new Binding(path)); return value; }
    private static Button DangerLink(string text, string commandPath)
    {
        var button = new Button
        {
            Content = text,
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 32,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Bind(Button.ForegroundProperty, new DynamicResourceExtension("Destructive"));
        button.Bind(Button.CommandProperty, new Binding(commandPath)
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ProductCatalogView) }
        });
        button.Bind(Button.CommandParameterProperty, new Binding());
        return button;
    }
    private static TextBlock Muted(string text) { var value = new TextBlock { Text = text }; value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return value; }
    private static TextBlock Text(string path, FontWeight? weight = null, string? resource = null, bool vertical = false) { var value = new TextBlock { FontWeight = weight ?? FontWeight.Normal, VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Top, TextWrapping = TextWrapping.Wrap }; value.Bind(TextBlock.TextProperty, new Binding(path)); if (resource is not null) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(resource)); return value; }
    private static T At<T>(T value, int row = 0, int column = 0) where T : Control { Grid.SetRow(value, row); Grid.SetColumn(value, column); return value; }
}
