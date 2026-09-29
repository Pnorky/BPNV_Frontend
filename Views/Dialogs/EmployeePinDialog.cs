using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Services;
using AvaloniaApp.Views.UI;

namespace AvaloniaApp.Views.Dialogs;

public sealed class EmployeePinDialog : Window
{
    private readonly TextBox _pin;
    private readonly TextBox _confirmation;
    private readonly TextBlock _validation;
    private readonly ActionButton _save;

    public EmployeePinDialog(EmployeeResponse employee)
    {
        Title = $"{(employee.HasPurchasePin ? "Reset" : "Set")} Purchase PIN - {employee.Name}";
        Width = 430; Height = 360; MinWidth = 430; MinHeight = 360;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        this.BindResource(BackgroundProperty, "Card");
        this.BindResource(ForegroundProperty, "Foreground");

        _pin = PinInput("Enter 4-digit PIN");
        _confirmation = PinInput("Confirm 4-digit PIN");
        _validation = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap };
        _validation.BindResource(TextBlock.ForegroundProperty, "Destructive");
        _save = new ActionButton(employee.HasPurchasePin ? "Reset PIN" : "Set PIN", ActionButtonVariant.Primary);
        _save.Click += (_, _) => { if (IsValid) { Confirmed = true; Close(); } };
        _pin.TextChanged += (_, _) => Validate();
        _confirmation.TextChanged += (_, _) => Validate();

        var cancel = new ActionButton("Cancel", ActionButtonVariant.Secondary);
        cancel.Click += (_, _) => Close();
        Content = new Grid
        {
            Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 18,
            Children =
            {
                new StackPanel { Spacing = 4, Children =
                {
                    Heading(employee.HasPurchasePin ? "Reset purchase PIN" : "Set purchase PIN"),
                    new TextBlock { Text = employee.ToString(), TextWrapping = TextWrapping.Wrap }
                } },
                At(new StackPanel { Spacing = 8, Children =
                {
                    Label("4-DIGIT PIN"), _pin, Label("CONFIRM PIN"), _confirmation, _validation
                } }, 1),
                At(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8, Children = { cancel, _save } }, 2)
            }
        };
        Validate();
    }

    public bool Confirmed { get; private set; }
    public string Pin => _pin.Text ?? "";
    private bool IsValid => IsPin(Pin) && Pin == (_confirmation.Text ?? "");

    private void Validate()
    {
        _validation.Text = !IsPin(Pin) ? "Enter exactly 4 numeric digits."
            : Pin != (_confirmation.Text ?? "") ? "The PINs do not match." : "";
        _validation.IsVisible = !IsValid;
        _save.IsEnabled = IsValid;
    }

    private static TextBox PinInput(string placeholder)
    {
        var input = new TextBox { PlaceholderText = placeholder, PasswordChar = '*', MaxLength = 4, MinHeight = 40 };
        input.AddHandler(InputElement.TextInputEvent, (_, args) =>
        {
            if (args.Text?.Any(character => character is < '0' or > '9') == true) args.Handled = true;
        });
        return input;
    }

    private static bool IsPin(string value) => value.Length == 4 && value.All(character => character is >= '0' and <= '9');
    private static TextBlock Heading(string text) { var value = new TextBlock { Text = text }; value.Classes.Add("h2"); return value; }
    private static TextBlock Label(string text) => new() { Text = text, FontSize = 11, FontWeight = FontWeight.SemiBold };
    private static T At<T>(T value, int row) where T : Control { Grid.SetRow(value, row); return value; }
}
