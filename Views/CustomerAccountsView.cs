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

public sealed class CustomerAccountsView : UserControl
{
    private bool _isCustomerDialogOpen;

    public CustomerAccountsView()
    {
        var customerTable = new PagedTable
        {
            ItemName = "customer", ItemNamePlural = "customers", PageSize = 10,
            MinTableWidth = 1050, IsSelectable = true, Height = 390
        };
        customerTable.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(CustomerAccountsViewModel.Customers)));
        customerTable.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(CustomerAccountsViewModel.SelectedCustomer)) { Mode = BindingMode.TwoWay });
        customerTable.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(CustomerAccountsViewModel.IsBusy)));
        customerTable.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(CustomerAccountsViewModel.ErrorMessage)));
        customerTable.Bind(PagedTable.IsFilteredProperty, new Binding(nameof(CustomerAccountsViewModel.IsFiltered)));
        customerTable.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(CustomerAccountsViewModel.LoadCommand)));
        customerTable.Bind(PagedTable.ClearFiltersCommandProperty, new Binding(nameof(CustomerAccountsViewModel.ClearFiltersCommand)));
        customerTable.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("ACCOUNT", item => item.AccountNumber, Star(1)));
        customerTable.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("CUSTOMER", item => item.Name, Star(1.8)));
        customerTable.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("CONTACT", item => item.ContactPerson ?? "-", Star(1.4)));
        customerTable.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("PHONE", item => item.Phone ?? "-", Star(1.1)));
        var money = PagedTableColumn.Create<CustomerResponse, string>("BALANCE", item => item.OutstandingDisplay, Star(1));
        money.HorizontalAlignment = HorizontalAlignment.Right;
        customerTable.Columns.Add(money);
        var lastPayment = PagedTableColumn.Create<CustomerResponse, string>("LAST PAYMENT", item => item.LastPaymentDisplay, Star(1.8));
        lastPayment.WrapText = true;
        customerTable.Columns.Add(lastPayment);
        customerTable.Columns.Add(PagedTableColumn.Create<CustomerResponse, string>("STATUS", item => item.Status, Star(.8)));
        customerTable.PropertyChanged += async (_, change) =>
        {
            if (change.Property != PagedTable.SelectedItemProperty ||
                customerTable.SelectedItem is not CustomerResponse customer ||
                _isCustomerDialogOpen || DataContext is not CustomerAccountsViewModel viewModel ||
                TopLevel.GetTopLevel(customerTable) is not Window owner) return;
            _isCustomerDialogOpen = true;
            viewModel.SelectedCustomer = customer;
            try
            {
                await new CustomerDetailsDialog(viewModel).ShowDialog(owner);
            }
            finally
            {
                viewModel.NewCustomerCommand.Execute(null);
                customerTable.SelectedItem = null;
                _isCustomerDialogOpen = false;
            }
        };

        var createCustomer = Card(CustomerForm());

        var customerList = Card(new StackPanel
        {
            Spacing = 10,
            Children =
            {
                Heading("Customer accounts"),
                Muted("Select a row once to open that customer's details and plate / unit numbers."),
                customerTable
            }
        });

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(30), Spacing = 14,
                Children = { Status(), createCustomer, customerList }
            }
        };
    }

    private static Control CustomerForm(bool showStatus = false)
    {
        var title = Bound(nameof(CustomerAccountsViewModel.CustomerFormTitle), 18, true);
        var status = MutedBound(nameof(CustomerAccountsViewModel.SelectedCustomerStatus));
        status.IsVisible = showStatus;
        var account = Text(nameof(CustomerAccountsViewModel.AccountNumberDisplay), "Loading...", 30, BindingMode.OneWay);
        account.IsReadOnly = true;
        var name = Text(nameof(CustomerAccountsViewModel.CustomerName), "Business or account name", 200);
        var address = Text(nameof(CustomerAccountsViewModel.BillingAddress), "Optional billing address", 500);
        var contact = Text(nameof(CustomerAccountsViewModel.ContactPerson), "Optional contact person", 160);
        var phone = Text(nameof(CustomerAccountsViewModel.Phone), "Optional phone", 50);
        var email = Text(nameof(CustomerAccountsViewModel.Email), "Optional email", 254);
        foreach (var control in new Control[] { account, name, address, contact, phone, email })
            control.Bind(IsEnabledProperty, new Binding(nameof(CustomerAccountsViewModel.CanManageCustomers)));

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*,2*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 10, RowSpacing = 10,
            Children =
            {
                Field("ACCOUNT NUMBER (AUTO-GENERATED)", account), At(Field("CUSTOMER NAME", name), 1), At(Field("BILLING ADDRESS", address), 2),
                At(Field("CONTACT PERSON", contact), row: 1), At(Field("PHONE", phone), 1, 1), At(Field("EMAIL", email), 2, 1)
            }
        };
        var save = new ActionButton("", ActionButtonVariant.Primary);
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(CustomerAccountsViewModel.CustomerSaveText)));
        save.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(nameof(CustomerAccountsViewModel.SaveCustomerCommand)));
        var deactivate = Button("Deactivate", nameof(CustomerAccountsViewModel.DeactivateCustomerCommand), ActionButtonVariant.Danger);
        deactivate.Bind(IsVisibleProperty, new Binding(nameof(CustomerAccountsViewModel.CanDeactivateCustomer)));
        var reactivate = Button("Reactivate", nameof(CustomerAccountsViewModel.ReactivateCustomerCommand));
        reactivate.Bind(IsVisibleProperty, new Binding(nameof(CustomerAccountsViewModel.CanReactivateCustomer)));
        var actionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { save, deactivate, reactivate }
        };
        var actions = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children = { At(actionButtons, column: 1) }
        };
        actions.Bind(IsVisibleProperty, new Binding(nameof(CustomerAccountsViewModel.CanManageCustomers)));
        return new StackPanel { Spacing = 10, Children = { title, status, fields, actions } };
    }

    private static Control VehicleSection()
    {
        var title = Bound(nameof(CustomerAccountsViewModel.VehicleSectionTitle), 18, true);
        var plate = Text(nameof(CustomerAccountsViewModel.PlateOrUnitNumber), "TJN 800 or BACKHOE", 50);
        var description = Text(nameof(CustomerAccountsViewModel.VehicleDescription), "Optional vehicle description", 160);
        var save = new ActionButton("", ActionButtonVariant.Primary);
        save.Bind(ContentControl.ContentProperty, new Binding(nameof(CustomerAccountsViewModel.VehicleSaveText)));
        save.Bind(Avalonia.Controls.Button.CommandProperty, new Binding(nameof(CustomerAccountsViewModel.SaveVehicleCommand)));
        save.VerticalAlignment = VerticalAlignment.Bottom;
        var clear = Button("New vehicle", nameof(CustomerAccountsViewModel.NewVehicleCommand));
        clear.VerticalAlignment = VerticalAlignment.Bottom;
        var editor = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*,Auto,Auto"), ColumnSpacing = 10,
            Children =
            {
                Field("PLATE / UNIT", plate), At(Field("DESCRIPTION", description), 1), At(save, 2), At(clear, 3)
            }
        };
        editor.Bind(IsVisibleProperty, new Binding(nameof(CustomerAccountsViewModel.CanManageCustomers)));

        var table = new PagedTable
        {
            ItemName = "vehicle", ItemNamePlural = "vehicles", PageSize = 10,
            IsSelectable = true, Height = 300, MinTableWidth = 650
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(CustomerAccountsViewModel.Vehicles)));
        table.Bind(PagedTable.SelectedItemProperty, new Binding(nameof(CustomerAccountsViewModel.SelectedVehicle)) { Mode = BindingMode.TwoWay });
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(CustomerAccountsViewModel.IsVehiclesLoading)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(CustomerAccountsViewModel.VehicleError)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(CustomerAccountsViewModel.RetryVehiclesCommand)));
        table.Columns.Add(PagedTableColumn.Create<CustomerVehicleResponse, string>("PLATE / UNIT", item => item.PlateOrUnitNumber, Star(1.2)));
        table.Columns.Add(PagedTableColumn.Create<CustomerVehicleResponse, string>("DESCRIPTION", item => item.Description ?? "-", Star(2)));
        table.Columns.Add(PagedTableColumn.Create<CustomerVehicleResponse, string>("STATUS", item => item.IsActive ? "Active" : "Inactive", Star(.8)));
        var actions = PagedTableColumn.Create<CustomerVehicleResponse, string>("ACTIONS", _ => "", Star(1.2), false);
        actions.CellTemplate = new FuncDataTemplate<CustomerVehicleResponse>((_, _) => VehicleActions(), true);
        table.Columns.Add(actions);

        return new StackPanel
        {
            Spacing = 12,
            Children =
            {
                title,
                Muted("All vehicles and equipment registered to this customer. Select a row to edit it."),
                editor,
                table
            }
        };
    }

    private static Control VehicleActions()
    {
        var deactivate = Button("Deactivate", "DataContext.DeactivateVehicleCommand", ActionButtonVariant.Danger,
            typeof(CustomerDetailsDialog));
        var reactivate = Button("Reactivate", "DataContext.ReactivateVehicleCommand", ActionButtonVariant.Secondary,
            typeof(CustomerDetailsDialog));
        deactivate.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        reactivate.Bind(Avalonia.Controls.Button.CommandParameterProperty, new Binding());
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { deactivate, reactivate } };
        panel.Bind(IsVisibleProperty, new Binding("DataContext.CanManageCustomers")
        { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(CustomerDetailsDialog) } });
        void Update()
        {
            if (panel.DataContext is not CustomerVehicleResponse vehicle) return;
            deactivate.IsVisible = vehicle.IsActive;
            reactivate.IsVisible = !vehicle.IsActive;
        }
        panel.DataContextChanged += (_, _) => Update();
        panel.AttachedToVisualTree += (_, _) => Update();
        return panel;
    }

    private sealed class CustomerDetailsDialog : Window
    {
        public CustomerDetailsDialog(CustomerAccountsViewModel viewModel)
        {
            Title = $"Customer details · {viewModel.SelectedCustomer?.Name}";
            Width = 1100;
            Height = 760;
            MinWidth = 850;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            DataContext = viewModel;

            var close = new ActionButton("Close", ActionButtonVariant.Secondary);
            close.Click += (_, _) => Close();
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Children = { close }
            };
            var content = new Grid
            {
                Margin = new Thickness(24),
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 16,
                Children =
                {
                    new ScrollViewer
                    {
                        Content = new Border
                        {
                            Padding = new Thickness(0, 0, 20, 0),
                            Child = new StackPanel
                            {
                                Spacing = 16,
                                Children = { CustomerForm(showStatus: true), new Separator(), VehicleSection() }
                            }
                        }
                    },
                    At(actions, row: 1)
                }
            };
            var border = new Border { Child = content, CornerRadius = new CornerRadius(0) };
            border.Classes.Add("theme-dialog");
            border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
            Content = border;
        }
    }

    private static ActionButton Button(string text, string command,
        ActionButtonVariant variant = ActionButtonVariant.Secondary, Type? ancestor = null)
    {
        var button = new ActionButton(text, variant, ActionButtonSize.Sm);
        button.Bind(Avalonia.Controls.Button.CommandProperty, ancestor is null ? new Binding(command) : new Binding(command)
        { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = ancestor } });
        return button;
    }

    private static TextBox Text(string path, string placeholder, int maxLength, BindingMode mode = BindingMode.TwoWay)
    {
        var control = new TextBox { PlaceholderText = placeholder, MaxLength = maxLength, Classes = { "form-input" } };
        control.Bind(TextBox.TextProperty, new Binding(path) { Mode = mode });
        return control;
    }

    private static StackPanel Field(string label, Control control) => new()
    {
        Spacing = 4,
        Children = { Muted(label), control }
    };

    private static Border Status()
    {
        var border = new Border { Padding = new Thickness(12, 8), Child = Bound(nameof(CustomerAccountsViewModel.StatusMessage)) };
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary"));
        return border;
    }

    private static Border Card(Control child)
    {
        var card = new Border { Padding = new Thickness(18), Child = child };
        card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        return card;
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontSize = 18, FontWeight = FontWeight.SemiBold };
    private static TextBlock Bound(string path, double? size = null, bool bold = false)
    {
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (size.HasValue) text.FontSize = size.Value;
        if (bold) text.FontWeight = FontWeight.SemiBold;
        text.Bind(TextBlock.TextProperty, new Binding(path));
        return text;
    }
    private static TextBlock Muted(string text) => Resource(new TextBlock { Text = text, FontSize = 11, TextWrapping = TextWrapping.Wrap }, TextBlock.ForegroundProperty, "MutedForeground");
    private static TextBlock MutedBound(string path) => Resource(Bound(path), TextBlock.ForegroundProperty, "MutedForeground");
    private static GridLength Star(double value) => new(value, GridUnitType.Star);
    private static T At<T>(T control, int column = 0, int row = 0) where T : Control { Grid.SetColumn(control, column); Grid.SetRow(control, row); return control; }
    private static T Resource<T>(T target, AvaloniaProperty property, string key) where T : AvaloniaObject { target.Bind(property, new DynamicResourceExtension(key)); return target; }
}
