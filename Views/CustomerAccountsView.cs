using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class CustomerAccountsView : UserControl
{
    public CustomerAccountsView()
    {
        var table = new PagedTable { ItemName = "customer", ItemNamePlural = "customers", PageSize = 10, MinTableWidth = 1050, IsSelectable = true };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(CustomerAccountsViewModel.Customers)));
        table.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(CustomerAccountsViewModel.SelectedCustomer)) { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(CustomerAccountsViewModel.IsBusy)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(CustomerAccountsViewModel.ErrorMessage)));
        table.Bind(PagedTable.IsFilteredProperty, new Binding(nameof(CustomerAccountsViewModel.IsFiltered)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(CustomerAccountsViewModel.LoadCommand)));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding(nameof(CustomerAccountsViewModel.ClearFiltersCommand)));
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("ACCOUNT", item => item.AccountNumber, new GridLength(120)));
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("CUSTOMER", item => item.Name, new GridLength(230)));
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("CONTACT", item => item.ContactPerson ?? "-", new GridLength(180)));
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("PHONE", item => item.Phone ?? "-", new GridLength(130)));
        var money = PagedTableColumn.Create<CustomerResponse, string>("BALANCE", item => item.OutstandingDisplay, new GridLength(130)); money.HorizontalAlignment = HorizontalAlignment.Right; table.Columns.Add(money);
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("LAST PAYMENT", item => item.LastPaymentDisplay, new GridLength(180)));
        table.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("STATUS", item => item.Status, new GridLength(100)));

        var customerForm = FormCard("Create customer", new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*,2*,*,*,Auto"), ColumnSpacing = 8,
            Children =
            {
                Field("Account number", Text("AccountNumber", "ACCT-001"), 0),
                Field("Customer name", Text("CustomerName", "Business or account name"), 1),
                Field("Billing address", Text("BillingAddress", "Optional address"), 2),
                Field("Contact person", Text("ContactPerson", "Optional"), 3),
                Field("Phone", Text("Phone", "Optional"), 4),
                Button("Create", "CreateCustomerCommand", 5)
            }
        });
        var vehicleForm = FormCard("Add plate or unit to selected customer", new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*,Auto"), ColumnSpacing = 8,
            Children =
            {
                Field("Plate / unit", Text("PlateOrUnitNumber", "TJN 800 or BACKHOE"), 0),
                Field("Description", Text("VehicleDescription", "Optional vehicle description"), 1),
                Button("Add", "CreateVehicleCommand", 2)
            }
        });
        var card = new Border { Padding = new Thickness(18), Child = table }; card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        var forms = new StackPanel { Spacing = 10, Children = { customerForm, vehicleForm } };
        forms.Bind(IsVisibleProperty, new Binding("CanManageCustomers"));
        Content = new Grid { Margin = new Thickness(30), RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 14,
            Children = { Status(), forms, At(card, 2) } };
        Grid.SetRow(forms, 1);
    }

    private static Border FormCard(string title, Control form)
    {
        var card = new Border { Padding = new Thickness(14), Child = new StackPanel { Spacing = 8, Children = { new TextBlock { Text = title, FontWeight = Avalonia.Media.FontWeight.SemiBold }, form } } };
        card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card")); return card;
    }
    private static Control Field(string label, Control control, int column) { var panel = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 11 }, control } }; Grid.SetColumn(panel, column); return panel; }
    private static TextBox Text(string path, string placeholder) { var control = new TextBox { PlaceholderText = placeholder, MinHeight = 38 }; control.Bind(TextBox.TextProperty, new Binding(path) { Mode = BindingMode.TwoWay }); return control; }
    private static Button Button(string text, string command, int column) { var button = new ActionButton(text, ActionButtonVariant.Primary) { VerticalAlignment = VerticalAlignment.Bottom }; button.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(command)); Grid.SetColumn(button, column); return button; }
    private static Border Status() { var text = new TextBlock(); text.Bind(TextBlock.TextProperty, new Binding("StatusMessage")); var border = new Border { Padding = new Thickness(12, 8), Child = text }; border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); return border; }
    private static T At<T>(T value, int row) where T : Control { Grid.SetRow(value, row); return value; }
}
