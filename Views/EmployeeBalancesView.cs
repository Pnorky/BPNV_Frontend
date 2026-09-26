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

public sealed class EmployeeBalancesView : UserControl
{
    public EmployeeBalancesView()
    {
        var table = new PagedTable
        {
            ItemName = "employee balance", ItemNamePlural = "employee balances", PageSize = 10,
            MinTableWidth = 1750, MinHeight = 440, IsSelectable = false
        };
        table.Bind(PagedTable.ItemsSourceProperty, new Binding(nameof(EmployeeBalancesViewModel.Balances)));
        table.Bind(PagedTable.IsLoadingProperty, new Binding(nameof(EmployeeBalancesViewModel.IsBusy)));
        table.Bind(PagedTable.ErrorMessageProperty, new Binding(nameof(EmployeeBalancesViewModel.ErrorMessage)));
        table.Bind(PagedTable.IsFilteredProperty, new Binding(nameof(EmployeeBalancesViewModel.IsFiltered)));
        table.Bind(PagedTable.RetryCommandProperty, new Binding(nameof(EmployeeBalancesViewModel.LoadCommand)));
        table.Bind(PagedTable.ClearFiltersCommandProperty, new Binding(nameof(EmployeeBalancesViewModel.ClearFiltersCommand)));
        table.Columns.Add(PagedTableColumn.Create<EmployeeBalanceResponse, string>("EMPLOYEE", item => item.EmployeeDisplay, new GridLength(250)));
        table.Columns.Add(Money("TOTAL PURCHASES", item => item.TotalPurchasesDisplay, 140));
        table.Columns.Add(Money("PAID AT PURCHASE", item => item.PaidAtPurchaseDisplay, 150));
        table.Columns.Add(Money("TOTAL OWED", item => item.OriginallyOwedDisplay, 130));
        table.Columns.Add(Money("PAYMENTS RECEIVED", item => item.RepaymentsDisplay, 155));
        table.Columns.Add(Money("BALANCE DUE", item => item.OutstandingBalanceDisplay, 140));
        table.Columns.Add(PagedTableColumn.Create<EmployeeBalanceResponse, string>("LAST PAYMENT", item => item.LastPaymentDisplay, new GridLength(180)));
        var status = PagedTableColumn.Create<EmployeeBalanceResponse, string>("ACCOUNT STATUS", item => item.Status, new GridLength(135));
        status.CellTemplate = new FuncDataTemplate<EmployeeBalanceResponse>((_, _) => StatusCell(), true);
        table.Columns.Add(status);
        var actions = PagedTableColumn.Create<EmployeeBalanceResponse, string>("AVAILABLE ACTIONS", _ => "", new GridLength(420), false);
        actions.HorizontalAlignment = HorizontalAlignment.Center;
        actions.CellTemplate = new FuncDataTemplate<EmployeeBalanceResponse>((_, _) => ActionsCell(), true);
        table.Columns.Add(actions);

        var card = new Border { Padding = new Thickness(18), Child = table };
        card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        Content = new Grid
        {
            Margin = new Thickness(30), RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 14,
            Children = { Metrics(), Status(), At(card, 2) }
        };
    }

    private static Control Metrics()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*"), ColumnSpacing = 12 };
        string[] labels = ["TOTAL PURCHASES", "PAID AT SALE", "ORIGINALLY OWED", "REPAYMENTS", "OUTSTANDING"];
        string[] paths = ["TotalPurchasesDisplay", "PaidAtPurchaseDisplay", "OriginallyOwedDisplay", "RepaymentsDisplay", "OutstandingDisplay"];
        for (var index = 0; index < labels.Length; index++)
        {
            var value = Bound(paths[index], 24, FontWeight.Bold);
            var label = new TextBlock { Text = labels[index], FontSize = 10, FontWeight = FontWeight.SemiBold };
            label.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
            var card = new Border { Padding = new Thickness(16), Child = new StackPanel { Spacing = 4, Children = { label, value } } };
            card.Classes.Add("theme-card"); card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
            Grid.SetColumn(card, index); grid.Children.Add(card);
        }
        return grid;
    }

    private static Border Status()
    {
        var border = new Border { Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(7), Child = Bound("StatusMessage") };
        border.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Secondary")); Grid.SetRow(border, 1); return border;
    }

    private static PagedTableColumn Money(string header, Func<EmployeeBalanceResponse, string> selector, double width)
    {
        var column = PagedTableColumn.Create(header, selector, new GridLength(width));
        column.HorizontalAlignment = HorizontalAlignment.Right;
        return column;
    }

    private static Control StatusCell()
    {
        var badge = new StatusBadge { VerticalAlignment = VerticalAlignment.Center };
        badge.Bind(StatusBadge.StatusProperty, new Binding(nameof(EmployeeBalanceResponse.Status)));
        return badge;
    }

    private static Control ActionsCell()
    {
        var purchases = Action("View purchases", "DataContext.ViewPurchasesCommand");
        var history = Action("Payment history", "DataContext.ShowPaymentHistoryCommand");
        var payment = new ActionButton("Pay balance", ActionButtonVariant.Primary, ActionButtonSize.Sm);
        BindCommand(payment, "DataContext.RecordPaymentCommand");
        payment.Bind(IsEnabledProperty, new Binding(nameof(EmployeeBalanceResponse.CanRecordPayment)));
        return new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Spacing = 6, Children = { purchases, history, payment } };
    }

    private static Button Action(string text, string command) { var button = new ActionButton(text, ActionButtonVariant.Secondary, ActionButtonSize.Sm); BindCommand(button, command); return button; }
    private static void BindCommand(Button button, string path)
    {
        button.Bind(Button.CommandProperty, new Binding(path) { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(EmployeeBalancesView) } });
        button.Bind(Button.CommandParameterProperty, new Binding());
    }
    private static TextBlock Bound(string path, double size = 14, FontWeight? weight = null) { var value = new TextBlock { FontSize = size, FontWeight = weight ?? FontWeight.Normal }; value.Bind(TextBlock.TextProperty, new Binding(path)); return value; }
    private static T At<T>(T value, int row) where T : Control { Grid.SetRow(value, row); return value; }
}
