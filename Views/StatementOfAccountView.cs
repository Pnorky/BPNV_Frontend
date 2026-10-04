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
        var from = new DatePicker(); from.Bind(DatePicker.SelectedDateProperty, new Binding("FromDate") { Mode = BindingMode.TwoWay });
        var to = new DatePicker(); to.Bind(DatePicker.SelectedDateProperty, new Binding("ToDate") { Mode = BindingMode.TwoWay });
        var preview = new ActionButton("Preview SOA", ActionButtonVariant.Primary); preview.Bind(Button.CommandProperty, new Binding("PreviewCommand"));
        var export = new ActionButton("Export PDF", ActionButtonVariant.Secondary); export.Bind(Button.CommandProperty, new Binding("ExportPdfCommand")); export.Bind(IsEnabledProperty, new Binding("HasStatement"));
        var filters = Card(new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*,*,Auto,Auto"), ColumnSpacing = 10, Children =
        {
            Field("Customer", customer, 0), Field("From", from, 1), Field("To", to, 2), At(preview, 3), At(export, 4)
        } });

        var table = new PagedTable { ItemName = "charge line", ItemNamePlural = "charge lines", PageSize = 10, MinTableWidth = 1050 };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding("Statement.Charges"));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("DATE", item => StoreDateTime.ToStoreTimeFromUtc(item.SoldAtUtc).ToString("MM/dd/yyyy"), new GridLength(130)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("PARTICULAR", item => item.Particular, new GridLength(220)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("INVOICE / REF", item => item.InvoiceOrReference, new GridLength(160)));
        table.Columns.Add(PagedTableColumn.Create<CustomerStatementLineResponse, string>("PLATE / UNIT", item => item.PlateOrUnitNumber ?? "-", new GridLength(140)));
        table.Columns.Add(Money("QTY / LITERS", item => item.Quantity.ToString("N2")));
        table.Columns.Add(Money("UNIT PRICE", item => item.UnitPrice.ToString("N2")));
        table.Columns.Add(Money("NET AMOUNT", item => $"₱{item.NetAmount:N2}"));
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
    private static PagedTableColumn Money(string header, Func<CustomerStatementLineResponse, string> selector) { var column = PagedTableColumn.Create(header, selector, new GridLength(120)); column.HorizontalAlignment = HorizontalAlignment.Right; return column; }
}
