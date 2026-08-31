using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace AvaloniaApp.Views.UI;

public sealed class AmountInput : TextBox
{
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<AmountInput, decimal?>(
            nameof(Value),
            defaultBindingMode: BindingMode.TwoWay,
            enableDataValidation: true);

    private bool _updatingText;
    private bool _updatingValue;
    private string _lastAcceptedText = string.Empty;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public AmountInput()
    {
        Classes.Add("form-input");
        MaxLength = 16;
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        TextInput += OnTextInput;
        LostFocus += (_, _) => UpdateText();
    }

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty && !_updatingValue)
            UpdateText();
        else if (change.Property == TextProperty && !_updatingText)
            UpdateValueFromText();
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        var current = Text ?? string.Empty;
        var selectionStart = Math.Min(SelectionStart, SelectionEnd);
        var selectionLength = Math.Abs(SelectionEnd - SelectionStart);
        var proposed = current.Remove(selectionStart, selectionLength).Insert(selectionStart, e.Text);
        if (!IsValidInput(proposed))
            e.Handled = true;
    }

    private void UpdateValueFromText()
    {
        if (_updatingText) return;

        var text = Text?.Trim() ?? string.Empty;
        if (!IsValidInput(text))
        {
            RestoreAcceptedText();
            return;
        }

        _lastAcceptedText = text;
        if (text.Length == 0)
        {
            SetInputValue(null);
            return;
        }

        if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            SetInputValue(amount);
    }

    private void SetInputValue(decimal? value)
    {
        _updatingValue = true;
        SetCurrentValue(ValueProperty, value);
        _updatingValue = false;
    }

    private void UpdateText()
    {
        _updatingText = true;
        Text = Value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
        _lastAcceptedText = Text;
        _updatingText = false;
    }

    private void RestoreAcceptedText()
    {
        _updatingText = true;
        Text = _lastAcceptedText;
        CaretIndex = Text.Length;
        _updatingText = false;
    }

    private static bool IsValidInput(string text)
    {
        if (text.Length == 0) return true;

        var decimalIndex = -1;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (char.IsDigit(character)) continue;
            if (character != '.' || decimalIndex >= 0) return false;
            decimalIndex = index;
        }

        return decimalIndex < 0 || text.Length - decimalIndex - 1 <= 2;
    }
}
