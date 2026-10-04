using Avalonia;
using Avalonia.Controls;
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
        method.Bind(SearchableSelect.ItemsSourceProperty, new Binding("PaymentMethods")); method.Bind(SearchableSelect.SelectedItemProperty, new Binding("SelectedPaymentMethod") { Mode = BindingMode.TwoWay });
        var reference = Text("ReferenceNumber", "Required for GCash");
        var note = Text("Note", "Optional payment note");
        var balance = new TextBlock { FontSize = 28, FontWeight = Avalonia.Media.FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right };
        balance.Bind(TextBlock.TextProperty, new Binding("OutstandingDisplay")); balance.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Primary"));
        var submit = new ActionButton("Record payment", ActionButtonVariant.Primary); submit.Bind(Button.CommandProperty, new Binding("SubmitCommand")); submit.Bind(IsEnabledProperty, new Binding("CanSubmit"));
        var card = new Border { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(22), Child = new StackPanel { Spacing = 12, Children =
        {
            new TextBlock { Text = "Customer payment", FontSize = 20, FontWeight = Avalonia.Media.FontWeight.SemiBold },
            new TextBlock { Text = "Payments are automatically applied to the oldest outstanding charge.", TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            Field("Customer", customer),
            new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { new TextBlock { Text = "OUTSTANDING BALANCE", VerticalAlignment = VerticalAlignment.Center }, At(balance, 1) } },
            Field("Amount", amount), Field("Payment method", method), Field("Reference number", reference), Field("Note", note), submit
        } } };
        card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        var status = new TextBlock(); status.Bind(TextBlock.TextProperty, new Binding("StatusMessage"));
        Content = new StackPanel { Margin = new Thickness(30), Spacing = 14, Children = { status, card } };
    }
    private static Control Field(string label, Control control) => new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12, FontWeight = Avalonia.Media.FontWeight.SemiBold }, control } };
    private static TextBox Text(string path, string placeholder) { var control = new TextBox { PlaceholderText = placeholder, MinHeight = 42 }; control.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay }); return control; }
    private static T At<T>(T value, int column) where T : Control { Grid.SetColumn(value, column); return value; }
}
