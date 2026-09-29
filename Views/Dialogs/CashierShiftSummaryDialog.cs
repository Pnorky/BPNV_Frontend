using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.ViewModels;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class CashierShiftSummaryDialog : Window
{
    public CashierShiftSummaryDialog(
        CashierShiftSessionResponse session,
        IReadOnlyList<CashierCashAdjustmentResponse>? adjustments = null,
        AdminCashierOperationsViewModel? viewModel = null)
    {
        DataContext = viewModel;
        var isActive = session.Status == ApiCashierShiftSessionStatus.Open;
        var statusText = isActive ? "Active" : "Completed";
        Title = $"{session.ShiftName} {statusText} - BPNV Convenience Store";
        Width = 760;
        Height = 590;
        MinWidth = 680;
        MinHeight = 560;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = $"{session.ShiftName} {statusText.ToLowerInvariant()}", FontSize = 32, FontWeight = FontWeight.SemiBold };
        var subtitle = new TextBlock { Text = $"{session.CashierName} | {session.BusinessDate:MMMM d, yyyy}", FontSize = 18 };
        subtitle.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
         var details = new Grid
         {
             ColumnDefinitions = new ColumnDefinitions("180,*"),
             RowDefinitions = new RowDefinitions(session.IsEmergencySelfOpened ? "Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto" : "Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 10
        };
         AddRow(details, 0, "Scheduled", session.ScheduledDisplay);
         AddRow(details, 1, "Actual time", session.ActualDisplay);
         AddRow(details, 2, "Elapsed hours", session.WorkedDisplay);
         AddRow(details, 3, "Transactions", (session.TransactionCount ?? 0).ToString("N0"));
         AddRow(details, 4, "Total sales", CashierShiftFormatting.Money(session.TotalSales));
         AddRow(details, 5, "Cash refunds / payouts", $"{CashierShiftFormatting.Money(session.CashRefunds)} / {CashierShiftFormatting.Money(session.CashPayouts)}");
         AddRow(details, 6, "Assignment origin", session.OriginDisplay);
         if (session.IsEmergencySelfOpened)
             AddRow(details, 7, "Emergency reason", session.EmergencyReason ?? "-");

         var regularTable = new Grid
         {
             ColumnDefinitions = new ColumnDefinitions("*,150"),
             RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"),
             RowSpacing = 0
         };
         AddSectionLabel(regularTable, 0, "Regular sales breakdown");
         AddTableHeader(regularTable, 1);
         AddTableRow(regularTable, 2, "Regular sales", CashierShiftFormatting.Money((session.RegularCashSales ?? 0) + (session.RegularGCashSales ?? 0)));
         AddTableRow(regularTable, 3, "Cash sales", CashierShiftFormatting.Money(session.RegularCashSales));
         AddTableRow(regularTable, 4, "GCash sales", CashierShiftFormatting.Money(session.RegularGCashSales));

         var employeeTable = new Grid
         {
             ColumnDefinitions = new ColumnDefinitions("*,150"),
             RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
             RowSpacing = 0
         };
         AddSectionLabel(employeeTable, 0, "Employee sales breakdown");
         AddTableHeader(employeeTable, 1);
         AddTableRow(employeeTable, 2, "Employee purchases", CashierShiftFormatting.Money(session.EmployeeSales));
         AddTableRow(employeeTable, 3, "Paid at checkout", CashierShiftFormatting.Money(session.EmployeePaidSales));
         AddTableRow(employeeTable, 4, "Added to balance (owed)", CashierShiftFormatting.Money(session.EmployeeOwedSales));
         AddSectionLabel(employeeTable, 5, "Employee balance payments received");
         AddTableHeader(employeeTable, 6);
         AddTableRow(employeeTable, 7, "Cash payments", CashierShiftFormatting.Money(session.CashEmployeeDebtRepayments));
         AddTableRow(employeeTable, 8, "GCash payments", CashierShiftFormatting.Money(session.GCashEmployeeDebtRepayments));

         var cashToRemit = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*") };
         AddRow(cashToRemit, 0, "Cash to remit", CashierShiftFormatting.Money(session.ExpectedRemittance), true);

        var floatText = new TextBlock
        {
            Text = $"Return the ₱{session.OpeningCashFloat:N2} opening cash for change separately. Do not include it in sales remittance.",
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 650
        };
        var floatCard = new Border { Padding = new Thickness(14), Child = floatText, CornerRadius = new CornerRadius(7) };
        floatCard.BindResource(Border.BackgroundProperty, "Secondary");
        var note = new TextBlock
        {
            Text = $"Please hand {CashierShiftFormatting.Money(session.ExpectedRemittance)} in sales cash to the Admin. The Admin will record the actual amount received.",
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 650
        };
        var requests = new StackPanel { Spacing = 8 };
        if (adjustments is { Count: > 0 })
        {
            requests.Children.Add(new TextBlock { Text = "Cash adjustment requests", FontSize = 17, FontWeight = FontWeight.SemiBold });
            if (viewModel is not null && adjustments.Any(item => item.Status == ApiCashAdjustmentStatus.Pending))
            {
                var reviewNote = new TextBox
                {
                    PlaceholderText = "Optional Admin review note",
                    Classes = { "form-input" }
                };
                reviewNote.Bind(TextBox.TextProperty, new Binding("AdjustmentReviewNote") { Mode = Avalonia.Data.BindingMode.TwoWay });
                requests.Children.Add(reviewNote);
            }
            foreach (var adjustment in adjustments)
            {
                var request = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    RowDefinitions = new RowDefinitions("Auto,Auto"),
                    ColumnSpacing = 12,
                    Children =
                    {
                        new TextBlock { Text = $"{adjustment.Type} | {CashierShiftFormatting.Money(adjustment.Amount)}", FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = adjustment.Status.ToString(), FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right },
                        new TextBlock { Text = adjustment.Note, TextWrapping = TextWrapping.Wrap, FontSize = 14 }
                    }
                };
                Grid.SetColumn(request.Children[1], 1);
                Grid.SetRow(request.Children[2], 1);
                Grid.SetColumnSpan(request.Children[2], 2);
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                if (viewModel is not null && adjustment.Status == ApiCashAdjustmentStatus.Pending)
                {
                    var approve = new Button { Content = "Approve", Classes = { "primary" } };
                    approve.Command = viewModel.ApproveAdjustmentCommand;
                    approve.CommandParameter = adjustment;
                    var reject = new Button { Content = "Reject", Classes = { "danger" } };
                    reject.Command = viewModel.RejectAdjustmentCommand;
                    reject.CommandParameter = adjustment;
                    actions.Children.Add(approve);
                    actions.Children.Add(reject);
                }

                var requestContent = new StackPanel { Spacing = 8, Children = { request, actions } };
                requests.Children.Add(new Border { Padding = new Thickness(10), Child = requestContent, CornerRadius = new CornerRadius(6) });
            }
        }

        var remittance = new StackPanel { Spacing = 8 };
        if (viewModel?.Detail?.Session.Status == ApiCashierShiftSessionStatus.ClosedPendingRemittance)
        {
            remittance.Children.Add(new TextBlock { Text = "Record remittance", FontSize = 17, FontWeight = FontWeight.SemiBold });
            remittance.Children.Add(new TextBlock
            {
                Text = "Enter the actual sales cash received. The opening float is recorded separately.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14
            });

            var amount = new AmountInput { PlaceholderText = "Actual sales cash remittance" };
            amount.Bind(AmountInput.ValueProperty, new Binding("ActualRemittance") { Mode = BindingMode.TwoWay });
            remittance.Children.Add(amount);

            var returned = new CheckBox { Content = "Opening cash float was returned or replaced" };
            returned.Bind(ToggleButton.IsCheckedProperty, new Binding("CashFloatReturned") { Mode = BindingMode.TwoWay });
            remittance.Children.Add(returned);

            var remittanceNoteInput = new TextBox
            {
                PlaceholderText = "Required for shortage, overage, or unreturned float",
                AcceptsReturn = true,
                MinHeight = 64,
                TextWrapping = TextWrapping.Wrap,
                Classes = { "form-input" }
            };
            remittanceNoteInput.Bind(TextBox.TextProperty, new Binding("RemittanceNote") { Mode = BindingMode.TwoWay });
            remittance.Children.Add(remittanceNoteInput);

            var validation = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            validation.Bind(TextBlock.TextProperty, new Binding("RemittanceValidationMessage"));
            remittance.Children.Add(validation);

            var record = new Button { Content = "Record remittance", Classes = { "primary" }, HorizontalAlignment = HorizontalAlignment.Left };
            record.Bind(Button.CommandProperty, new Binding("RecordRemittanceCommand"));
            remittance.Children.Add(record);
        }
        var close = new Button
        {
            Content = "Acknowledge",
            IsDefault = true,
            FontSize = 14,
            Width = 120,
            Height = 32,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        };
        close.Classes.Add("primary");
        close.Click += (_, _) => Close();

        var content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 16,
              Children = { new StackPanel { Children = { title, subtitle } }, details, regularTable, employeeTable, cashToRemit, requests, remittance, floatCard, note, close }
        };
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = content
        };
        var border = new Border { Child = scroll };
        border.Classes.Add("theme-dialog");
        border.BindResource(Border.BackgroundProperty, "Card");
        Content = border;
    }

    private static void AddRow(Grid grid, int row, string label, string value, bool emphasize = false)
    {
        var key = new TextBlock { Text = label, FontSize = 15 };
        key.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var amount = new TextBlock { Text = value, FontSize = emphasize ? 19 : 15, FontWeight = emphasize ? FontWeight.Bold : FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = emphasize ? HorizontalAlignment.Right : HorizontalAlignment.Left };
        if (emphasize) amount.BindResource(TextBlock.ForegroundProperty, "Primary");
        Grid.SetRow(key, row);
        Grid.SetRow(amount, row);
        Grid.SetColumn(amount, 1);
        grid.Children.Add(key);
        grid.Children.Add(amount);
    }

    private static void AddSectionLabel(Grid grid, int row, string text)
    {
        var heading = new TextBlock { Text = text, FontSize = 14, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0) };
        heading.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        Grid.SetRow(heading, row);
        Grid.SetColumnSpan(heading, 2);
        grid.Children.Add(heading);
    }

    private static void AddTableHeader(Grid grid, int row)
    {
        var detail = new TextBlock { Text = "DETAIL", FontSize = 12, FontWeight = FontWeight.SemiBold };
        var amount = new TextBlock { Text = "AMOUNT", FontSize = 12, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
        detail.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        amount.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        Grid.SetRow(detail, row);
        Grid.SetRow(amount, row);
        Grid.SetColumn(amount, 1);
        grid.Children.Add(detail);
        grid.Children.Add(amount);
    }

    private static void AddTableRow(Grid grid, int row, string label, string value)
    {
        var detail = new Border { Padding = new Thickness(8, 5), BorderThickness = new Thickness(1), Child = new TextBlock { Text = label, FontSize = 15 } };
        var amount = new Border { Padding = new Thickness(8, 5), BorderThickness = new Thickness(1), Child = new TextBlock { Text = value, FontSize = 15, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Right } };
        detail.BindResource(Border.BorderBrushProperty, "Border");
        amount.BindResource(Border.BorderBrushProperty, "Border");
        Grid.SetRow(detail, row);
        Grid.SetRow(amount, row);
        Grid.SetColumn(amount, 1);
        grid.Children.Add(detail);
        grid.Children.Add(amount);
    }
}
