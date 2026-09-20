using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views;

public sealed class CashierShiftView : UserControl
{
    public CashierShiftView()
    {
        var status = Text("StatusMessage");
        status.TextWrapping = TextWrapping.Wrap;
        var statusHost = new Border { Padding = new Thickness(14, 11), Child = status };
        statusHost.BindResource(Border.BackgroundProperty, "Secondary");
        statusHost.CornerRadius = new CornerRadius(7);

        var refresh = new ActionButton("Refresh", ActionButtonVariant.Secondary);
        refresh.Bind(Button.CommandProperty, new Binding("RefreshCommand"));
        var clockIn = new ActionButton("Clock in", ActionButtonVariant.Primary);
        clockIn.Bind(Button.CommandProperty, new Binding("ClockInCommand"));
        clockIn.Bind(Visual.IsVisibleProperty, new Binding("CanClockIn"));
        var clockOut = new ActionButton("Clock out", ActionButtonVariant.Danger);
        clockOut.Bind(Button.CommandProperty, new Binding("ClockOutCommand"));
        clockOut.Bind(Visual.IsVisibleProperty, new Binding("CanClockOut"));
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { refresh, clockIn, clockOut }
        };

        var assignment = Detail("ASSIGNED WINDOW", "AssignmentWindow", 0, 0);
        var actual = Detail("ACTUAL SESSION", "ActualWindow", 1, 0);
        var opening = Detail("OPENING FLOAT", "OpeningFloatDisplay", 0, 1);
        var shift = Detail("CURRENT STATUS", "Shift.StatusDisplay", 1, 1);
        var details = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("1.2*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 24,
            RowSpacing = 20,
            Children = { assignment, actual, opening, shift }
        };

        var blockReason = Text("BlockReason", "MutedForeground");
        blockReason.TextWrapping = TextWrapping.Wrap;
        var blockHost = new Border { Padding = new Thickness(14), Child = blockReason };
        blockHost.Bind(Visual.IsVisibleProperty, new Binding("HasBlockReason"));
        blockHost.BindResource(Border.BackgroundProperty, "Muted");
        blockHost.CornerRadius = new CornerRadius(7);

        var card = new Border
        {
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 20,
                Children =
                {
                    new StackPanel { Spacing = 5, Children = { Heading("Terminal clock"), Muted("Your schedule and all financial times come from the store server.") } },
                    statusHost,
                    details,
                    blockHost,
                    actions
                }
            }
        };
        card.Classes.Add("theme-card");
        card.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));

        var adjustmentCard = AdjustmentCard();
        adjustmentCard.Bind(Visual.IsVisibleProperty, new Binding("HasOpenSession"));
        Content = new ScrollViewer
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Spacing = 16,
                Children = { card, adjustmentCard }
            }
        };
    }

    private static Border AdjustmentCard()
    {
        var type = new ComboBox();
        type.Classes.Add("form-select");
        type.Bind(ItemsControl.ItemsSourceProperty, new Binding("AdjustmentTypes"));
        type.Bind(SelectingItemsControl.SelectedItemProperty, new Binding("SelectedAdjustmentType") { Mode = BindingMode.TwoWay });
        var amount = new AmountInput { PlaceholderText = "0.00" };
        amount.Bind(AmountInput.ValueProperty, new Binding("AdjustmentAmount") { Mode = BindingMode.TwoWay });
        var note = new TextBox { PlaceholderText = "Required business reason", AcceptsReturn = true, MinHeight = 70, TextWrapping = TextWrapping.Wrap };
        note.MaxLength = 1000;
        note.Classes.Add("form-input");
        note.Bind(TextBox.TextProperty, new Binding("AdjustmentNote") { Mode = BindingMode.TwoWay });
        var reference = new TextBox { PlaceholderText = "Optional receipt or reference" };
        reference.MaxLength = 160;
        reference.Classes.Add("form-input");
        reference.Bind(TextBox.TextProperty, new Binding("AdjustmentReference") { Mode = BindingMode.TwoWay });
        var submit = new ActionButton("Submit for Admin review", ActionButtonVariant.Primary);
        submit.Bind(Button.CommandProperty, new Binding("RequestAdjustmentCommand"));
        var validation = Text("AdjustmentValidationMessage", "Destructive");
        validation.TextWrapping = TextWrapping.Wrap;

        var list = new ItemsControl();
        list.Bind(ItemsControl.ItemsSourceProperty, new Binding("Adjustments"));
        list.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<CashierCashAdjustmentResponse>((item, _) =>
        {
            var row = new Border
            {
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 6),
                CornerRadius = new CornerRadius(7),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new StackPanel { Children = { new TextBlock { Text = $"{item.Type} | {item.AmountDisplay}", FontWeight = FontWeight.SemiBold }, new TextBlock { Text = item.Note, TextWrapping = TextWrapping.Wrap } } },
                        At(new TextBlock { Text = item.Status.ToString(), FontWeight = FontWeight.SemiBold }, 1)
                    }
                }
            };
            row.BindResource(Border.BackgroundProperty, "Muted");
            return row;
        }, true);

        var card = new Border
        {
            Padding = new Thickness(22),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new StackPanel { Spacing = 5, Children = { Heading("Cash adjustments"), Muted("Refunds and cash payouts affect remittance only after Admin approval. Store expenses remain separate."), Muted("Pending Cash refund or payout requests must be reviewed by Admin before clock-out.") } },
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 12, Children = { Field("TYPE", type), At(Field("AMOUNT", amount), 1) } },
                    Field("NOTE", note),
                    Field("RECEIPT / REFERENCE", reference),
                    validation,
                    submit,
                    Heading("Requests for this session"),
                    list
                }
            }
        };
        card.Classes.Add("theme-card");
        card.BindResource(Border.BackgroundProperty, "Card");
        return card;
    }

    private static StackPanel Field(string label, Control control) => new() { Spacing = 5, Children = { new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.SemiBold }, control } };

    private static Control Detail(string label, string path, int column, int row)
    {
        var value = Text(path);
        value.FontSize = 15;
        value.FontWeight = FontWeight.SemiBold;
        value.TextWrapping = TextWrapping.Wrap;
        var panel = new StackPanel { Spacing = 5, Children = { Muted(label), value } };
        Grid.SetColumn(panel, column);
        Grid.SetRow(panel, row);
        return panel;
    }

    private static TextBlock Heading(string value) { var text = new TextBlock { Text = value }; text.Classes.Add("h2"); return text; }
    private static TextBlock Muted(string value) { var text = new TextBlock { Text = value }; text.BindResource(TextBlock.ForegroundProperty, "MutedForeground"); return text; }
    private static TextBlock Text(string path, string? resource = null) { var text = new TextBlock(); text.Bind(TextBlock.TextProperty, new Binding(path)); if (resource is not null) text.BindResource(TextBlock.ForegroundProperty, resource); return text; }
    private static T At<T>(T value, int column = 0, int row = 0) where T : Control { Grid.SetColumn(value, column); Grid.SetRow(value, row); return value; }
}
