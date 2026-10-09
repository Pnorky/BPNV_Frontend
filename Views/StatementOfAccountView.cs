using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class StatementOfAccountView : UserControl
{
    public StatementOfAccountView()
    {
        var customer = new SearchableSelect { PlaceholderText = "Select customer", SearchTextSelector = item => (item as CustomerResponse)?.SearchText ?? "" };
        customer.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Customers")); customer.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedCustomer") { Mode = BindingMode.TwoWay });
        var period = new DateRangePicker
        {
            PlaceholderText = "Select statement period",
            MinWidth = 380,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        period.Bind(DateRangePicker.StartDateProperty, new Binding("FromDate") { Mode = BindingMode.TwoWay });
        period.Bind(DateRangePicker.EndDateProperty, new Binding("ToDate") { Mode = BindingMode.TwoWay });
        var preview = new ActionButton("Preview SOA", ActionButtonVariant.Primary); preview.Bind(Button.CommandProperty, new Binding("PreviewCommand"));
        var export = new ActionButton("Export PDF", ActionButtonVariant.Secondary); export.Bind(Button.CommandProperty, new Binding("ExportPdfCommand")); export.Bind(IsEnabledProperty, new Binding("HasStatement"));
        var filters = Card(new Grid { ColumnDefinitions = new ColumnDefinitions("2*,1.6*,Auto,Auto"), ColumnSpacing = 12, Children =
        {
            Field("Customer", customer, 0), Field("Statement period", period, 1), At(preview, 2), At(export, 3)
        } });

        var table = new PagedTable { ItemName = "charge line", ItemNamePlural = "charge lines", PageSize = 10, MinTableWidth = 1050 };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding("Statement.Charges"));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("DATE", item => StoreDateTime.ToStoreTimeFromUtc(item.ChargedAtUtc).ToString("MMMM d, yyyy"), Star(1.4)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("PARTICULAR", item => item.Particular, Star(1.8)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("INVOICE / REF", item => item.InvoiceOrReference, Star(1.4)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("PLATE / UNIT", item => item.PlateOrUnitNumber ?? "-", Star(1.3)));
        table.Columns.Add(Money("QUANTITY", item => item.Quantity.ToString("N3"), 1));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("UNIT", item => item.Unit, Star(0.7)));
        table.Columns.Add(Money("UNIT PRICE", item => item.UnitPrice.ToString("N2"), 1));
        table.Columns.Add(Money("NET AMOUNT", item => $"₱{item.NetAmount:N2}", 1.2));
        var tableCard = Card(table); Grid.SetRow(tableCard, 3);
        var status = new TextBlock(); status.Bind(TextBlock.TextProperty, new Binding("StatusMessage")); Grid.SetRow(status, 1);
        var metrics = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = 10,
            Children = { Metric("PREVIOUS BALANCE", "PreviousBalanceDisplay", 0), Metric("CURRENT CHARGES", "CurrentChargesDisplay", 1), Metric("PERIOD PAYMENTS", "PeriodPaymentsDisplay", 2), Metric("TOTAL BALANCE", "TotalBalanceDisplay", 3) } }; Grid.SetRow(metrics, 2);
        Content = new Grid { Margin = new Thickness(30), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), RowSpacing = 14, Children = { filters, status, metrics, tableCard } };
    }
    private static Border Card(Control child) { var card = new Border { Padding = new Thickness(16), Child = child }; card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return card; }
    private static Control Field(string label, Control control, int column) { var panel = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 11 }, control } }; Grid.SetColumn(panel, column); return panel; }
    private static T At<T>(T control, int column) where T : Control { control.VerticalAlignment = VerticalAlignment.Bottom; Grid.SetColumn(control, column); return control; }
    private static Border Metric(string label, string path, int column) { var value = new TextBlock { FontSize = 19, FontWeight = Avalonia.Media.FontWeight.Bold }; value.Bind(TextBlock.TextProperty, new Binding(path)); var card = Card(new StackPanel { Spacing = 3, Children = { new TextBlock { Text = label, FontSize = 10 }, value } }); Grid.SetColumn(card, column); return card; }
    private static PagedTableColumn Money(string header, Func<CustomerStatementLineResponse, string> selector, double width) { var column = PagedTableColumn.Create(header, selector, Star(width)); column.HorizontalAlignment = HorizontalAlignment.Right; return column; }
    private static GridLength Star(double value) => new(value, GridUnitType.Star);
}
