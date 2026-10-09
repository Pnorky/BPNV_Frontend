using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class CustomerPaymentView : UserControl
{
    public CustomerPaymentView()
    {
        var customer = new SearchableSelect { PlaceholderText = "Select customer account", SearchTextSelector = item => (item as CustomerResponse)?.SearchText ?? "" };
        customer.Bind(SearchableSelect.ItemsSourceProperty, new Binding("Customers"));
        customer.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedCustomer") { Mode = BindingMode.TwoWay });
        var amount = new AmountInput(); amount.Bind(AmountInput.ValueProperty, new Binding("Amount") { Mode = BindingMode.TwoWay });
        var method = new SearchableSelect { PlaceholderText = "Select payment method" };
        method.ItemTemplate = new FuncDataTemplate<ApiCustomerPaymentMethod>((value, _) =>
            new TextBlock { Text = PaymentMethodDisplay(value) });
        method.Bind(SearchableSelect.ItemsSourceProperty, new Binding("PaymentMethods")); method.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedPaymentMethod") { Mode = BindingMode.TwoWay });
        var receipt = Field("Receipt number", Text("ReceiptNumber", "Enter the issued receipt number"));
        receipt.Bind(IsVisibleProperty, new Binding("IsCash"));
        var reference = Field("Reference number", Text("ReferenceNumber", "GCash, transfer, or card reference"));
        reference.Bind(IsVisibleProperty, new Binding("RequiresReference"));
        var checkFields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            ColumnSpacing = 12,
            Children =
            {
                Field("Bank name", Text("CheckBank", "Issuing bank")),
                At(Field("Check number", Text("CheckNumber", "Required check number")), 1)
            }
        };
        checkFields.Bind(IsVisibleProperty, new Binding("IsCheck"));
        var note = Text("Note", "Optional payment note");
        var submit = new ActionButton("Record payment", ActionButtonVariant.Primary) { HorizontalAlignment = HorizontalAlignment.Right };
        submit.Bind(Button.CommandProperty, new Binding("SubmitCommand")); submit.Bind(IsEnabledProperty, new Binding("CanSubmit"));
        var details = new StackPanel { Spacing = 12, Children = { receipt, reference, checkFields } };
        var accountName = SummaryValue("SelectedCustomer.Name");
        var outstanding = SummaryValue("OutstandingDisplay", true);
        var paymentAmount = SummaryValue("PaymentAmountDisplay");
        var remaining = SummaryValue("RemainingBalanceDisplay", true);
        var cashMessage = new TextBlock { FontSize = 12, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        cashMessage.Bind(TextBlock.TextProperty, new Binding("CashAvailabilityMessage"));
        cashMessage.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var separator = new Border { Height = 1 };
        separator.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Border"));
        var summary = new Border
        {
            Padding = new Thickness(22), CornerRadius = new CornerRadius(12), VerticalAlignment = VerticalAlignment.Stretch,
            Child = new StackPanel { Spacing = 16, Children =
            {
                new TextBlock { Text = "Payment summary", FontSize = 18, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                SummarySection("SELECTED ACCOUNT", accountName),
                SummaryRow("Outstanding balance", outstanding),
                SummaryRow("Payment amount", paymentAmount),
                separator,
                SummaryRow("Remaining balance", remaining),
                cashMessage
            } }
        };
        summary.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));
        var form = new StackPanel { Spacing = 16, Children =
        {
            Field("Customer", customer),
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 16,
                Children = { Field("Amount", amount), At(Field("Payment method", method), 1) }
            },
            details,
            Field("Note", note),
            submit
        } };
        var card = new Border { HorizontalAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(22), Child = new StackPanel { Spacing = 16, Children =
        {
            new TextBlock { Text = "Customer payment", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold },
            new TextBlock { Text = "Payments are automatically applied to the oldest outstanding charge.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("3*,2*"), ColumnSpacing = 24,
                Children = { form, At(summary, 1) }
            }
        } } };
        card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        var status = new TextBlock(); status.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        Content = new StackPanel { Margin = new Thickness(30), Spacing = 14, Children = { status, card } };
    }
    private static Control Field(string label, Control control) => new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12, FontWeight = Avalonia.Media.FontWeight.SemiBold }, control } };
    private static TextBox Text(string path, string placeholder) { var control = new TextBox { PlaceholderText = placeholder, Classes = { "form-input" } }; control.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay }); return control; }
    private static TextBlock SummaryValue(string path, bool emphasize = false)
    {
        var value = new TextBlock { FontSize = emphasize ? 22 : 14, FontWeight = emphasize ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        value.Bind(TextBlock.TextProperty, new Binding(path));
        if (emphasize) value.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Primary"));
        return value;
    }
    private static Control SummarySection(string label, Control value) => new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 11 }, value } };
    private static Control SummaryRow(string label, Control value) => new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, Children = { new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center }, At(value, 1) } };
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
    private static string PaymentMethodDisplay(ApiCustomerPaymentMethod value) => value == ApiCustomerPaymentMethod.BankTransfer
        ? "Bank Transfer"
        : value.ToString();
}
