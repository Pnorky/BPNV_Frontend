using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class CustomerAccountChargesView : UserControl
{
    public CustomerAccountChargesView()
    {
        var status = Resource(new Border
        {
            Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(7),
            Child = BoundText("StatusMessage")
        }, Border.BackgroundProperty, "Secondary");

        var warning = Resource(new Border
        {
            Padding = new Thickness(14, 11), CornerRadius = new CornerRadius(7),
            Child = new TextBlock
            {
                Text = "Accounts Receivable only: saving creates customer debt. It does not create products, deduct stock, or affect cashier shifts.",
                TextWrapping = TextWrapping.Wrap
            }
        }, Border.BackgroundProperty, "Secondary");

        var customer = new SearchableSelect
        {
            PlaceholderText = "Select customer account",
            SearchTextSelector = item => (item as CustomerResponse)?.SearchText ?? ""
        };
        Bind(customer, SearchableSelect.ItemsSourceProperty, "Customers");
        Bind(customer, SearchableSelect.SelectedItemProperty, "SelectedCustomer");
        var vehicle = new SearchableSelect { PlaceholderText = "Optional plate or unit" };
        Bind(vehicle, SearchableSelect.ItemsSourceProperty, "Vehicles");
        Bind(vehicle, SearchableSelect.SelectedItemProperty, "SelectedVehicle");
        var date = new ShadcnDateTimePicker { ShowTime = false, DateLabel = "CHARGE DATE" };
        Bind(date, ShadcnDateTimePicker.SelectedDateProperty, "ChargeDate");
        var reference = Input("InvoiceReference", "Invoice or delivery reference", 100);
        var note = Input("Note", "Optional charge note", 500);
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 16, RowSpacing = 12,
            Children =
            {
                Field("CUSTOMER", customer), At(Field("CHARGE DATE", date), 1),
                At(Field("INVOICE / REFERENCE", reference), row: 1), At(Field("PLATE / UNIT", vehicle), 1, 1),
                At(Field("NOTE", note), row: 2, span: 2)
            }
        };

        var addLine = Button("Add line", "AddLineCommand");
        var lineHeading = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { new StackPanel { Children = { Heading("Charge lines"), Muted("Enter manual particulars, quantities, units, and prices.") } }, At(addLine, 1) }
        };
        var lines = new ItemsControl();
        Bind(lines, ItemsControl.ItemsSourceProperty, "Lines");
        lines.ItemTemplate = new FuncDataTemplate<CustomerAccountChargeLineViewModel>((_, _) => ChargeLineRow(), true);
        var total = BoundText("ChargeTotalDisplay");
        total.FontSize = 28; total.FontWeight = FontWeight.Bold;
        Resource(total, TextBlock.ForegroundProperty, "Primary");
        var save = Button("Save account charge", "SaveCommand", true);
        Bind(save, IsEnabledProperty, "CanSave");
        var summary = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 16,
            Children =
            {
                new TextBlock { Text = "Backend validation recalculates the final total before posting.", VerticalAlignment = VerticalAlignment.Center },
                At(new StackPanel { Spacing = 2, Children = { Muted("CHARGE TOTAL"), total } }, 1), At(save, 2)
            }
        };
        var form = Card(new StackPanel { Spacing = 16, Children = { header, lineHeading, lines, summary } });

        var table = new PagedTable
        {
            ItemName = "account charge", ItemNamePlural = "account charges", PageSize = 10,
            IsSelectable = true, Height = 390, MinTableWidth = 1000
        };
        Bind(table, PagedTable.ItemsSourceProperty, "Charges");
        Bind(table, PagedTable.SelectedItemProperty, "SelectedCharge");
        Bind(table, PagedTable.IsLoadingProperty, "IsHistoryLoading");
        Bind(table, PagedTable.ErrorMessageProperty, "HistoryError");
        Bind(table, PagedTable.RetryCommandProperty, "RetryHistoryCommand");
        Bind(table, PagedTable.PageSizeProperty, "HistoryPageSize");
        Bind(table, PagedTable.ExternalPageProperty, "HistoryPage");
        Bind(table, PagedTable.ExternalTotalCountProperty, "HistoryTotalCount");
        Bind(table, PagedTable.ExternalPreviousCommandProperty, "PreviousHistoryCommand");
        Bind(table, PagedTable.ExternalNextCommandProperty, "NextHistoryCommand");
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("CHARGE NO.", item => item.ChargeNumber, Star(1.1)));
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("DATE", item => item.ChargeDateDisplay, Star(1)));
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("CUSTOMER", item => item.CustomerName, Star(1.6)));
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("INVOICE / REF", item => item.InvoiceReference, Star(1.3)));
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("PLATE / UNIT", item => item.PlateOrUnitNumber ?? "-", Star(1.1)));
        var totalColumn = PagedTableColumn.Create<CustomerAccountChargeResponse, decimal>("TOTAL", item => item.Total, Star(1));
        totalColumn.ValueSelector = item => ((CustomerAccountChargeResponse)item).TotalDisplay;
        totalColumn.HorizontalAlignment = HorizontalAlignment.Right;
        table.Columns.Add(totalColumn);
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("STATUS", item => item.StatusDisplay, Star(0.8)));
        table.Columns.Add(PagedTableColumn.Create<CustomerAccountChargeResponse, string>("CREATED BY", item => item.CreatedByName, Star(1.1)));

        var voidReason = Input("VoidReason", "Required reason for voiding the selected unallocated charge", 500);
        var voidButton = Button("Void selected charge", "VoidSelectedCommand");
        Bind(voidButton, IsEnabledProperty, "CanVoid");
        var voidRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12,
            Children = { Field("VOID REASON", voidReason), At(voidButton, 1) }
        };
        var history = Card(new StackPanel
        {
            Spacing = 12,
            Children = { Heading("Recent account charges"), Muted("Select an unallocated posted charge to void it."), table, voidRow }
        });

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(30), Spacing = 14,
                Children = { status, warning, form, history }
            }
        };
    }

    private static Control ChargeLineRow()
    {
        var remove = Button("Remove", "DataContext.RemoveLineCommand", ancestor: typeof(CustomerAccountChargesView));
        remove.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        var quantity = Number("Quantity", "0.000", 0.001m, 999999999999999.999m, 0.001m);
        var price = Number("UnitPrice", "0.0000", 0, 99999999999999.9999m, 0.01m);
        var amount = BoundText("AmountDisplay"); amount.FontWeight = FontWeight.SemiBold;
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,0.8*,0.8*,1*,1*,Auto"), ColumnSpacing = 10,
            Children =
            {
                Field("PARTICULAR", Input("Particular", "Diesel or lubricant", 200)),
                At(Field("QUANTITY", quantity), 1), At(Field("UNIT", Input("Unit", "L or Can", 30)), 2),
                At(Field("UNIT PRICE", price), 3), At(Field("AMOUNT", new Border { MinHeight = 42, Child = amount }), 4), At(remove, 5)
            }
        };
        remove.VerticalAlignment = VerticalAlignment.Bottom;
        return Resource(new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 12), Child = row },
            Border.BorderBrushProperty, "Border");
    }

    private static TextBox Input(string path, string placeholder, int maxLength = 0)
    {
        var value = new TextBox { PlaceholderText = placeholder, MaxLength = maxLength, Classes = { "form-input" } };
        Bind(value, TextBox.TextProperty, path); return value;
    }
    private static NumberField Number(string path, string format, decimal minimum, decimal maximum, decimal increment)
    {
        var value = new NumberField { Minimum = minimum, Maximum = maximum, FormatString = format, Increment = increment };
        Bind(value, NumberField.ValueProperty, path); return value;
    }
    private static ActionButton Button(string text, string command, bool primary = false, Type? ancestor = null)
    {
        var value = new ActionButton(text, primary ? ActionButtonVariant.Primary : ActionButtonVariant.Secondary);
        value.Bind(Avalonia.Controls.Button.CommandProperty, ancestor is null ? new Binding(command) : new Binding(command)
        { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = ancestor } });
        return value;
    }
    private static StackPanel Field(string label, Control control) => new() { Spacing = 4, Children = { Muted(label), control } };
    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 18, FontWeight = FontWeight.SemiBold };
    private static TextBlock Muted(string text) => Resource(new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap }, TextBlock.ForegroundProperty, "MutedForeground");
    private static TextBlock BoundText(string path) { var value = new TextBlock(); Bind(value, TextBlock.TextProperty, path); return value; }
    private static Border Card(Control child) { var value = new Border { Padding = new Thickness(20), Child = child, HorizontalAlignment = HorizontalAlignment.Stretch }; value.Classes.Add("theme-card"); return Resource(value, Border.BackgroundProperty, "Card"); }
    private static T At<T>(T control, int column = 0, int row = 0, int span = 1) where T : Control { Grid.SetColumn(control, column); Grid.SetRow(control, row); Grid.SetColumnSpan(control, span); return control; }
    private static T Bind<T>(T target, AvaloniaProperty property, string path) where T : AvaloniaObject { target.Bind(property, new Binding(path)); return target; }
    private static T Resource<T>(T target, AvaloniaProperty property, string key) where T : AvaloniaObject { target.Bind(property, new DynamicResourceExtension(key)); return target; }
    private static GridLength Star(double value) => new(value, GridUnitType.Star);
}
