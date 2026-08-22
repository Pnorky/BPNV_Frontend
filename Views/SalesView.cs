using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public class SalesView : UserControl
{
    private readonly TextBox _scanner;
    private SalesViewModel? _viewModel;

    public SalesView()
    {
        _scanner = new TextBox { PlaceholderText = "Scan piece or package barcode, then press Enter" };
        _scanner.Classes.Add("form-input");
        _scanner.Bind(TextBox.TextProperty, new Binding("ScannerText"));
        _scanner.KeyDown += OnScannerKeyDown;

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,410"),
            ColumnSpacing = 16,
            Children = { Card(Catalog()), At(Card(CurrentSale()), 1) }
        };
        Grid.SetRow(body, 2);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            Margin = new Thickness(30),
            RowSpacing = 14,
            Children = { Header(), At(_scanner, row: 1), body }
        };
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => FocusScanner();
    }

    private static Control Header()
    {
        var title = new TextBlock { Text = "New sale" }; title.Classes.Add("h1");
        var pricing = new SearchableSelect { MinWidth = 150, PlaceholderText = "Pricing type" };
        pricing.Bind(SearchableSelect.ItemsSourceProperty, new Binding("CustomerTypes"));
        pricing.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedCustomerType"));
        Grid.SetColumn(pricing, 1);
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, StaticMuted("Server-priced package-aware point of sale") } },
                pricing
            }
        };
    }

    private static Control Catalog()
    {
        var search = new TextBox { PlaceholderText = "Search current API catalog..." }; search.Classes.Add("search");
        search.Bind(TextBox.TextProperty, new Binding("SearchText"));
        var list = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredProducts"));
        list.ItemTemplate = new FuncDataTemplate<PosProductResponse>((_, _) => ProductRow(), true);
        Grid.SetRow(list, 1);
        return new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 12, Children = { search, list } };
    }

    private static Control ProductRow()
    {
        var add = new Button { Content = "Add piece" }; add.Classes.Add("primary");
        add.Bind(Avalonia.Controls.Button.CommandProperty, AncestorCommand("AddProductCommand"));
        add.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        Grid.SetColumn(add, 2);
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(8, 13),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,120,Auto"),
                ColumnSpacing = 12,
                Children =
                {
                    new StackPanel { Children = { Text("Name", FontWeight.SemiBold), Text("CatalogDetails", resource: "MutedForeground") } },
                    At(new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { Text("RegularPrice", FontWeight.SemiBold, format: "Selling ₱{0:N2}"), Text("EmployeePrice", resource: "MutedForeground", format: "Employee ₱{0:N2}") } }, 1),
                    add
                }
            }
        };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Control CurrentSale()
    {
        var clear = Button("Clear", "ClearSaleCommand", false); Grid.SetColumn(clear, 1);
        var heading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { new StackPanel { Children = { Heading("Current sale"), Text("CartSummary", resource: "MutedForeground") } }, clear }
        };
        var cart = new ListBox { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        cart.Bind(ItemsControl.ItemsSourceProperty, new Binding("Cart"));
        cart.ItemTemplate = new FuncDataTemplate<ApiCartLine>((_, _) => CartRow(), true);
        Grid.SetRow(cart, 1);
        var footer = Footer(); Grid.SetRow(footer, 2);
        return new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12, Children = { heading, cart, footer } };
    }

    private static Control CartRow()
    {
        var remove = Button("Remove", "RemoveLineCommand", true, ancestor: true); remove.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding()); Grid.SetColumn(remove, 1);
        var minus = Button("-", "DecreaseQuantityCommand", false, ancestor: true);
        minus.Width = 36;
        minus.Padding = new Thickness(0);
        minus.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        var count = Text("Count", FontWeight.Bold); count.HorizontalAlignment = HorizontalAlignment.Center; count.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(count, 1);
        var plus = Button("+", "IncreaseQuantityCommand", false, ancestor: true);
        plus.Width = 36;
        plus.Padding = new Thickness(0);
        plus.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        Grid.SetColumn(plus, 2);
        var amount = Text("AmountDisplay", FontWeight.Bold); amount.VerticalAlignment = VerticalAlignment.Center; amount.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(amount, 3);
        var border = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 12),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new StackPanel { Children = { Text("ProductName", FontWeight.SemiBold), Text("UnitLabel", resource: "MutedForeground") } }, remove } },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("36,36,36,*"), ColumnSpacing = 8, Children = { minus, count, plus, amount } },
                    Text("ConversionDisplay", resource: "MutedForeground")
                }
            }
        };
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        return border;
    }

    private static Control Footer()
    {
        var total = Text("TotalDisplay", FontWeight.Bold); total.FontSize = 24; Grid.SetColumn(total, 1);
        var totalHost = new Border { Padding = new Thickness(14), CornerRadius = new CornerRadius(7), Child = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "Client estimate", VerticalAlignment = VerticalAlignment.Center }, total } } };
        totalHost.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Muted"));
        var status = Text("StatusMessage", resource: "MutedForeground"); status.TextWrapping = TextWrapping.Wrap;
        return new StackPanel { Spacing = 10, Children = { totalHost, status, Button("Complete sale", "CompleteSaleCommand", true) } };
    }

    private async void OnScannerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not SalesViewModel viewModel) return;
        e.Handled = true;
        await viewModel.ScanBarcodeAsync();
        FocusScanner();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null) _viewModel.ScannerFocusRequested -= OnScannerFocusRequested;
        _viewModel = DataContext as SalesViewModel;
        if (_viewModel is not null) _viewModel.ScannerFocusRequested += OnScannerFocusRequested;
    }
    private void OnScannerFocusRequested(object? sender, EventArgs e) => FocusScanner();
    private void FocusScanner() => Dispatcher.UIThread.Post(() => _scanner.Focus());
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child }; value.Classes.Add("theme-card"); value.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return value; }
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h2"); return value; }
    private static TextBlock StaticMuted(string value) { var text = new TextBlock { Text = value }; text.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground")); return text; }
    private static TextBlock Text(string path, FontWeight? weight = null, string? resource = null, string? format = null) { var value = new TextBlock { FontWeight = weight ?? FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path) { StringFormat = format }); if (resource is not null) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension(resource)); return value; }
    private static Button Button(string text, string command, bool primary, bool ancestor = false) { var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary); value.Bind(Avalonia.Controls.Button.CommandProperty, ancestor ? AncestorCommand(command) : new Binding(command)); return value; }
    private static Binding AncestorCommand(string command) => new($"DataContext.{command}") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBox) } };
    private static T At<T>(T value, int column = 0, int row = 0) where T : Control { Grid.SetColumn(value, column); Grid.SetRow(value, row); return value; }
}
