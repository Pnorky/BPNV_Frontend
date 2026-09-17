using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Services;

namespace AvaloniaApp.Views.Dialogs;

public sealed class CashierShiftSummaryDialog : Window
{
    public CashierShiftSummaryDialog(CashierShiftSessionResponse session)
    {
        Title = $"{session.ShiftName} Completed - BPNV Convenience Store";
        Width = 600;
        Height = 610;
        MinWidth = 600;
        MinHeight = 610;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        var title = new TextBlock { Text = $"{session.ShiftName} completed", FontSize = 22, FontWeight = FontWeight.SemiBold };
        var subtitle = new TextBlock { Text = $"{session.CashierName} | {session.BusinessDate:MMMM d, yyyy}", FontSize = 13 };
        subtitle.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto,Auto"),
            RowSpacing = 11
        };
        AddRow(details, 0, "Scheduled", session.ScheduledDisplay);
        AddRow(details, 1, "Actual time", session.ActualDisplay);
        AddRow(details, 2, "Elapsed hours", session.WorkedDisplay);
        AddRow(details, 3, "Transactions", (session.TransactionCount ?? 0).ToString("N0"));
        AddRow(details, 4, "Total sales", CashierShiftFormatting.Money(session.TotalSales));
        AddRow(details, 5, "Cash sales", CashierShiftFormatting.Money(session.CashSales));
        AddRow(details, 6, "GCash", CashierShiftFormatting.Money(session.GCashSales));
        AddRow(details, 7, "Cash refunds / payouts", $"{CashierShiftFormatting.Money(session.CashRefunds)} / {CashierShiftFormatting.Money(session.CashPayouts)}");
        AddRow(details, 8, "Cash to remit", CashierShiftFormatting.Money(session.ExpectedRemittance), true);

        var floatText = new TextBlock
        {
            Text = $"Return the ₱{session.OpeningCashFloat:N2} opening cash for change separately. Do not include it in sales remittance.",
            TextWrapping = TextWrapping.Wrap
        };
        var floatCard = new Border { Padding = new Thickness(14), Child = floatText, CornerRadius = new CornerRadius(7) };
        floatCard.BindResource(Border.BackgroundProperty, "Secondary");
        var note = new TextBlock
        {
            Text = $"Please hand {CashierShiftFormatting.Money(session.ExpectedRemittance)} in sales cash to the Admin. The Admin will record the actual amount received.",
            TextWrapping = TextWrapping.Wrap
        };
        var close = new Button { Content = "Acknowledge", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Classes.Add("primary");
        close.Click += (_, _) => Close();

        var content = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 18,
            Children = { new StackPanel { Children = { title, subtitle } }, details, floatCard, note, close }
        };
        var border = new Border { Child = content };
        border.Classes.Add("theme-dialog");
        border.BindResource(Border.BackgroundProperty, "Card");
        Content = border;
    }

    private static void AddRow(Grid grid, int row, string label, string value, bool emphasize = false)
    {
        var key = new TextBlock { Text = label, FontSize = 13 };
        key.BindResource(TextBlock.ForegroundProperty, "MutedForeground");
        var amount = new TextBlock { Text = value, FontSize = emphasize ? 18 : 13, FontWeight = emphasize ? FontWeight.Bold : FontWeight.SemiBold };
        if (emphasize) amount.BindResource(TextBlock.ForegroundProperty, "Primary");
        Grid.SetRow(key, row);
        Grid.SetRow(amount, row);
        Grid.SetColumn(amount, 1);
        grid.Children.Add(key);
        grid.Children.Add(amount);
    }
}
