using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;

namespace AvaloniaApp.Views.UI;

public sealed class PageTopBar : UserControl
{
    public static readonly StyledProperty<object?> PageProperty =
        AvaloniaProperty.Register<PageTopBar, object?>(nameof(Page));

    private readonly StackPanel _filters;
    private readonly StackPanel _actions;

    public event EventHandler<string>? NavigationRequested;

    public object? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public PageTopBar()
    {
        var title = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        title.Classes.Add("h2");
        title.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.PageTitle)));

        _filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_filters, 1);

        _actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_actions, 2);

        var storeClock = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        storeClock.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.StoreClockDisplay)));
        var shiftStatus = new TextBlock
        {
            Width = 330,
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        shiftStatus.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.ShiftStatusDisplay)));
        shiftStatus.Bind(Visual.IsVisibleProperty, new Binding(nameof(DashboardViewModel.IsCashier)));
        shiftStatus.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var shiftTooltip = new TextBlock();
        shiftTooltip.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.ShiftStatusDisplay)));
        ToolTip.SetTip(shiftStatus, shiftTooltip);
        var shiftButton = new Button
        {
            Content = "Shift",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            MinWidth = 68,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        shiftButton.Classes.Add("secondary");
        shiftButton.Bind(Visual.IsVisibleProperty, new Binding(nameof(DashboardViewModel.IsCashier)));
        shiftButton.Click += (_, _) => NavigationRequested?.Invoke(this, "CashierShift");
        var notificationBadge = new Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        notificationBadge.Bind(Border.BackgroundProperty, new DynamicResourceExtension("DestructiveRed"));
        var notificationCount = new TextBlock
        {
            FontSize = 9,
            FontWeight = FontWeight.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        notificationCount.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.AdminNotificationBadgeDisplay)));
        notificationCount.BindResource(TextBlock.ForegroundProperty, "DestructiveForeground");
        notificationBadge.Child = notificationCount;
        notificationBadge.Bind(Visual.IsVisibleProperty, new Binding(nameof(DashboardViewModel.HasAdminUnreadNotifications)));
        var notificationContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { new TextBlock { Text = "Notifications", VerticalAlignment = VerticalAlignment.Center }, notificationBadge }
        };
        var notificationsButton = new Button { FontSize = 11, Padding = new Thickness(8, 4), Margin = new Thickness(4, 0, 0, 0), Content = notificationContent };
        notificationsButton.Classes.Add("secondary");
        notificationsButton.Bind(Visual.IsVisibleProperty, new Binding(nameof(DashboardViewModel.IsAdmin)));
        notificationsButton.Click += (_, _) => NavigationRequested?.Invoke(this, "AdminNotifications");
        var clockText = new StackPanel
        {
            Width = 330,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { storeClock, shiftStatus }
        };
        var clockPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Width = 406,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { clockText, shiftButton }
        };
        Grid.SetColumn(_actions, 2);

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { title, _filters, _actions, clockPanel, notificationsButton }
        };
        Grid.SetColumn(clockPanel, 3);
        Grid.SetColumn(notificationsButton, 4);

        var border = new Border
        {
            Height = 64,
            Padding = new Thickness(24, 10),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = layout
        };
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        Content = border;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageProperty) RebuildActions();
    }

    private void RebuildActions()
    {
        _filters.Children.Clear();
        _actions.Children.Clear();
        if (Page is null) return;

        foreach (var control in CreateActions(Page))
        {
            control.DataContext = Page;
            var host = Equals(control.Tag, "filter") ? _filters : _actions;
            host.Children.Add(control);
        }
    }

    private IEnumerable<Control> CreateActions(object page)
    {
        switch (page)
        {
            case DashboardPageViewModel:
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Secondary);
                break;
            case AdminNotificationsViewModel:
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Secondary);
                yield return Button("Mark all read", "MarkAllReadCommand", ActionButtonVariant.Primary);
                break;
            case AdminCashierOperationsViewModel:
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Secondary);
                break;
            case SalesViewModel:
                var pricing = new SearchableSelect { PlaceholderText = "Pricing type", Width = 160 };
                Bind(pricing, SearchableSelect.ItemsSourceProperty, "CustomerTypes");
                Bind(pricing, SearchableSelect.SelectedItemProperty, "SelectedCustomerType");
                yield return pricing;
                var employee = new SearchableSelect { PlaceholderText = "Select employee", Width = 300 };
                Bind(employee, SearchableSelect.ItemsSourceProperty, "Employees");
                Bind(employee, SearchableSelect.SelectedItemProperty, "SelectedEmployee");
                Bind(employee, Visual.IsVisibleProperty, "IsEmployeeSale");
                yield return employee;
                break;
            case CashierSalesViewModel:
                var salesRange = new SearchableSelect { PlaceholderText = "Sales range", Width = 150 };
                Bind(salesRange, SearchableSelect.ItemsSourceProperty, "Ranges");
                Bind(salesRange, SearchableSelect.SelectedItemProperty, "SelectedRange");
                yield return salesRange;
                 var salesDates = new DateRangePicker { Width = 380, PlaceholderText = "Custom date range" };
                Bind(salesDates, DateRangePicker.StartDateProperty, "FromDate");
                Bind(salesDates, DateRangePicker.EndDateProperty, "ToDate");
                yield return salesDates;
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Secondary);
                break;
            case ProductCatalogViewModel:
                yield return Search("Search products...", "SearchText", 300);
                var type = new SearchableSelect { PlaceholderText = "Item type", Width = 170, Tag = "filter" };
                Bind(type, SearchableSelect.ItemsSourceProperty, "TypeFilters");
                Bind(type, SearchableSelect.SelectedItemProperty, "SelectedTypeFilter");
                type.ItemTemplate = new FuncDataTemplate<ProductTypeFilterOption>((_, _) => BoundText("Label"), true);
                yield return type;
                yield return Button("Refresh", "LoadCommand", ActionButtonVariant.Secondary);
                var addProduct = Button("Add product", null, ActionButtonVariant.Primary);
                addProduct.Click += (_, _) => NavigationRequested?.Invoke(this, "InventoryAddProduct");
                yield return addProduct;
                break;
            case AddProductViewModel:
                yield return Button("Create product", "CreateProductCommand", ActionButtonVariant.Primary);
                break;
            case StockReceivingViewModel:
                yield return Button("Receive into bodega", "SubmitReceiptCommand", ActionButtonVariant.Primary);
                break;
            case BatchReceivingViewModel:
                yield return BoundButton("Clear", "ClearDraftCommand", "CanEdit", ActionButtonVariant.Secondary);
                yield return BoundButton("Review batch", "ReviewBatchCommand", "CanReview", ActionButtonVariant.Primary);
                yield return BoundButton("Receive into bodega", "CommitBatchCommand", "CanCommit", ActionButtonVariant.Primary);
                break;
            case DeliveryHistoryViewModel:
                yield return FilterButton("Filters", page, DeliveryFilters());
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Secondary);
                break;
            case ExcelInventoryImportViewModel:
                yield return Button("Blank template", "ExportBlankTemplateCommand", ActionButtonVariant.Secondary);
                yield return VisibleButton("Prefilled template", "ExportPrefilledTemplateCommand", "IsLoaded", ActionButtonVariant.Secondary);
                yield return Button("Open .xlsx", "OpenWorkbookCommand", ActionButtonVariant.Primary);
                yield return VisibleBoundButton("Validate", "ValidateCommand", "IsLoaded", "CanValidate", ActionButtonVariant.Secondary);
                yield return VisibleBoundButton("Import", "ImportCommand", "IsLoaded", "CanImport", ActionButtonVariant.Primary);
                break;
            case SuppliersViewModel:
                yield return Search("Search suppliers...", "SearchText", 300);
                yield return Button("Refresh", "LoadCommand", ActionButtonVariant.Secondary);
                yield return Button("Add supplier", "CreateSupplierCommand", ActionButtonVariant.Primary);
                break;
            case ApiStockMovementsViewModel:
                yield return FilterButton("History filters", page, MovementFilters());
                break;
            case ReportsViewModel:
                var reportFilters = FilterButton("Filters", page, ReportFilters());
                Bind(reportFilters, Visual.IsVisibleProperty, "HasReportFilters");
                yield return reportFilters;
                yield return Button("Export PDF", "ExportPdfCommand", ActionButtonVariant.Secondary);
                yield return Button("Export Excel", "ExportExcelCommand", ActionButtonVariant.Secondary);
                yield return Button("Refresh", "RefreshCommand", ActionButtonVariant.Primary);
                break;
            case EmployeesViewModel:
                yield return Search("Search employees...", "SearchText", 280);
                yield return Button("Refresh", "LoadCommand", ActionButtonVariant.Secondary);
                yield return Button("Add employee", "CreateEmployeeCommand", ActionButtonVariant.Primary);
                break;
            case UsersViewModel:
                yield return Search("Search users...", "SearchText", 250);
                var status = new ComboBox { Width = 130, Tag = "filter" };
                status.Classes.Add("form-select");
                Bind(status, ItemsControl.ItemsSourceProperty, "StatusFilters");
                Bind(status, SelectingItemsControl.SelectedItemProperty, "SelectedStatusFilter");
                yield return status;
                yield return Button("Refresh", "LoadCommand", ActionButtonVariant.Secondary);
                yield return Button("New user", "NewUserCommand", ActionButtonVariant.Primary);
                break;
        }
    }

    private static Control DeliveryFilters()
    {
        var receipt = Search("Receipt or invoice number...", "ReceiptNumberSearch");
        receipt.Input.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key != Key.Enter || receipt.DataContext is not DeliveryHistoryViewModel viewModel) return;
            viewModel.ApplyFiltersCommand.Execute(null);
            eventArgs.Handled = true;
        };
        var supplier = new SearchableSelect { PlaceholderText = "All suppliers" };
        Bind(supplier, SearchableSelect.ItemsSourceProperty, "Suppliers");
        Bind(supplier, SearchableSelect.SelectedItemProperty, "SelectedSupplier");
        supplier.SearchTextSelector = item => item is SupplierResponse value ? value.Name : item.ToString() ?? "";
        var sort = new SelectDropdown();
        Bind(sort, SelectDropdown.ItemsSourceProperty, "SortOptions");
        Bind(sort, SelectDropdown.SelectedItemProperty, "SelectedSort");
        var dates = new DateRangePicker { PlaceholderText = "Delivery date range" };
        Bind(dates, DateRangePicker.StartDateProperty, "FromDate");
        Bind(dates, DateRangePicker.EndDateProperty, "ToDate");
        var supplierField = Field("SUPPLIER", supplier); Grid.SetColumn(supplierField, 1);
        var sortField = Field("ORDER", sort); Grid.SetRow(sortField, 1);
        var dateField = Field("DELIVERY DATE", dates); Grid.SetColumn(dateField, 1); Grid.SetRow(dateField, 1);
        var actions = Buttons(Button("Apply filters", "ApplyFiltersCommand", ActionButtonVariant.Primary), Button("Clear", "ClearFiltersCommand", ActionButtonVariant.Secondary));
        actions.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumnSpan(actions, 2); Grid.SetRow(actions, 2);
        return FilterPanel(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.15*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children = { Field("RECEIPT / INVOICE", receipt), supplierField, sortField, dateField, actions }
        });
    }

    private static Control ReportFilters()
    {
        var dates = new DateRangePicker { PlaceholderText = "Report date range" };
        Bind(dates, DateRangePicker.StartDateProperty, "FromDate");
        Bind(dates, DateRangePicker.EndDateProperty, "ToDate");
        Bind(dates, Visual.IsVisibleProperty, "IsDateFilteredReportTab");
        var type = new SelectDropdown();
        Bind(type, SelectDropdown.ItemsSourceProperty, "CustomerTypeOptions");
        Bind(type, SelectDropdown.SelectedItemProperty, "SelectedCustomerType");
        Bind(type, Visual.IsVisibleProperty, "IsSalesReportTab");
        var employee = new SearchableSelect { PlaceholderText = "All employees" };
        Bind(employee, SearchableSelect.ItemsSourceProperty, "Employees");
        Bind(employee, SearchableSelect.SelectedItemProperty, "SelectedEmployee");
        Bind(employee, Visual.IsVisibleProperty, "IsEmployeePurchasesTab");
        var cashier = new SearchableSelect { PlaceholderText = "All cashiers" };
        Bind(cashier, SearchableSelect.ItemsSourceProperty, "Cashiers");
        Bind(cashier, SearchableSelect.SelectedItemProperty, "SelectedCashier");
        cashier.SearchTextSelector = item => item is UserResponse user ? user.DisplayName : item?.ToString() ?? "";
        cashier.ItemTemplate = new FuncDataTemplate<UserResponse>((user, _) => new TextBlock { Text = user?.DisplayName ?? "" }, true);
        Bind(cashier, Visual.IsVisibleProperty, "ShowsCashierFilter");
        var dateField = Field("DATE RANGE", dates);
        Bind(dateField, Visual.IsVisibleProperty, "IsDateFilteredReportTab");
        var typeField = Field("SALES TYPE", type); Grid.SetColumn(typeField, 1);
        var employeeField = Field("EMPLOYEE", employee); Grid.SetColumnSpan(employeeField, 2); Grid.SetRow(employeeField, 1);
        var cashierField = Field("CASHIER", cashier); Grid.SetColumnSpan(cashierField, 2); Grid.SetRow(cashierField, 1);
        Bind(typeField, Visual.IsVisibleProperty, "IsSalesReportTab");
        Bind(employeeField, Visual.IsVisibleProperty, "IsEmployeePurchasesTab");
        Bind(cashierField, Visual.IsVisibleProperty, "ShowsCashierFilter");
        var actions = Buttons(Button("Apply filters", "RefreshCommand", ActionButtonVariant.Primary), Button("Clear employee", "ClearEmployeeFilterCommand", ActionButtonVariant.Secondary));
        actions.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumnSpan(actions, 2); Grid.SetRow(actions, 2);
        return FilterPanel(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.35*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children = { dateField, typeField, employeeField, cashierField, actions }
        });
    }

    private static Control MovementFilters()
    {
        var search = Search("Product, SKU, supplier...", "HistorySearchText");
        var movement = new SelectDropdown();
        Bind(movement, SelectDropdown.ItemsSourceProperty, "MovementTypes");
        Bind(movement, SelectDropdown.SelectedItemProperty, "SelectedMovementType");
        var sort = new SelectDropdown();
        Bind(sort, SelectDropdown.ItemsSourceProperty, "MovementSortOptions");
        Bind(sort, SelectDropdown.SelectedItemProperty, "SelectedMovementSort");
        var reference = Search("Reference or sale number...", "HistoryReference");
        var dates = new DateRangePicker { PlaceholderText = "Movement date range" };
        Bind(dates, DateRangePicker.StartDateProperty, "HistoryFromDate");
        Bind(dates, DateRangePicker.EndDateProperty, "HistoryToDate");
        var movementField = Field("MOVEMENT", movement); Grid.SetColumn(movementField, 1);
        var referenceField = Field("REFERENCE", reference); Grid.SetRow(referenceField, 1);
        var sortField = Field("ORDER", sort); Grid.SetColumn(sortField, 1); Grid.SetRow(sortField, 1);
        var dateField = Field("DATE RANGE", dates); Grid.SetRow(dateField, 2);
        var actions = Buttons(Button("Apply filters", "ApplyHistoryFiltersCommand", ActionButtonVariant.Primary), Button("Clear", "ClearHistoryFiltersCommand", ActionButtonVariant.Secondary));
        actions.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumnSpan(actions, 2); Grid.SetRow(actions, 3);
        return FilterPanel(new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.25*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 12,
            Children = { Field("SEARCH", search), movementField, referenceField, sortField, dateField, actions }
        });
    }

    private static Border FilterPanel(Control content)
    {
        var panel = new Border
        {
            Width = 560,
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            Child = content
        };
        panel.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        panel.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        panel.Bind(Border.CornerRadiusProperty, new DynamicResourceExtension("RadiusXl"));
        return panel;
    }

    private static Button FilterButton(string text, object page, Control content)
    {
        content.DataContext = page;
        var button = Button(text, null, ActionButtonVariant.Secondary);
        var flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            Content = content
        };
        flyout.FlyoutPresenterClasses.Add("filter-flyout");
        button.Click += (_, _) => flyout.ShowAt(button);
        return button;
    }

    private static StackPanel Field(string label, Control control)
    {
        var caption = new TextBlock { Text = label };
        caption.Classes.Add("form-label");
        return new StackPanel { Spacing = 5, Children = { caption, control } };
    }

    private static StackPanel Buttons(params Button[] buttons)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Bottom };
        foreach (var button in buttons) panel.Children.Add(button);
        return panel;
    }

    private static IconInput Search(string placeholder, string path, double? width = null)
    {
        var search = new IconInput("Search", placeholder) { Tag = "filter" };
        if (width is not null) search.Width = width.Value;
        Bind(search.Input, TextBox.TextProperty, path);
        return search;
    }

    private static ActionButton Button(string text, string? command, ActionButtonVariant variant)
    {
        var button = new ActionButton(text, variant, ActionButtonSize.Sm);
        if (command is not null) Bind(button, Avalonia.Controls.Button.CommandProperty, command);
        return button;
    }

    private static ActionButton BoundButton(string text, string command, string enabledPath, ActionButtonVariant variant)
    {
        var button = Button(text, command, variant);
        Bind(button, Avalonia.Controls.Button.IsEnabledProperty, enabledPath);
        return button;
    }

    private static ActionButton VisibleButton(string text, string command, string visiblePath, ActionButtonVariant variant)
    {
        var button = Button(text, command, variant);
        Bind(button, Visual.IsVisibleProperty, visiblePath);
        return button;
    }

    private static ActionButton VisibleBoundButton(string text, string command, string visiblePath, string enabledPath, ActionButtonVariant variant)
    {
        var button = BoundButton(text, command, enabledPath, variant);
        Bind(button, Visual.IsVisibleProperty, visiblePath);
        return button;
    }

    private static TextBlock BoundText(string path)
    {
        var text = new TextBlock();
        Bind(text, TextBlock.TextProperty, path);
        return text;
    }

    private static void Bind(AvaloniaObject target, AvaloniaProperty property, string path) =>
        target.Bind(property, new Binding(path));
}
