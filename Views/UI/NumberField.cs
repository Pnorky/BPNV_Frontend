using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaApp.Services;
using AvaloniaApp.Views.Controls;

namespace AvaloniaApp.Views.UI;

public sealed class NumberField : Border
{
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Minimum), decimal.MinValue);

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Maximum), decimal.MaxValue);

    public static readonly StyledProperty<decimal> IncrementProperty =
        AvaloniaProperty.Register<NumberField, decimal>(nameof(Increment), 1m);

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<NumberField, string>(nameof(FormatString), "0");

    public static readonly StyledProperty<bool> IsInputValidProperty =
        AvaloniaProperty.Register<NumberField, bool>(nameof(IsInputValid), true);

    private readonly TextBox _input;
    private readonly Border _incrementButton;
    private readonly Border _decrementButton;
    private bool _updatingText;
    private bool _updatingValueFromText;
    private bool _settingInvalidValue;

    protected override Type StyleKeyOverride => typeof(Border);

    public NumberField()
    {
        Classes.Add("number-field");

        _input = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            FocusAdorner = null,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Left
        };
        _input.Classes.Add("number-field-input");
        _input.TextInput += OnTextInput;
        _input.PropertyChanged += OnInputPropertyChanged;
        _input.KeyDown += OnInputKeyDown;
        _input.LostFocus += (_, _) =>
        {
            CommitText();
            OnChildLostFocus();
        };
        _input.GotFocus += (_, _) => OnChildGotFocus();

        _incrementButton = StepButton("ChevronUp", "Increase value");
        _decrementButton = StepButton("ChevronDown", "Decrease value");
        _incrementButton.PointerPressed += (_, e) =>
        {
            Step(Increment);
            e.Handled = true;
        };
        _decrementButton.PointerPressed += (_, e) =>
        {
            Step(-Increment);
            e.Handled = true;
        };

        var divider = new Border { Height = 1 };
        divider.Bind(BackgroundProperty, new DynamicResourceExtension("Input"));
        var stepper = new Border
        {
            Margin = new Thickness(3),
            CornerRadius = new CornerRadius(5),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto,*"),
                Children =
                {
                    _incrementButton,
                    At(divider, 1),
                    At(_decrementButton, 2)
                }
            }
        };
        stepper.Classes.Add("number-field-stepper");
        Grid.SetColumn(stepper, 1);

        Child = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,34"),
            Children = { _input, stepper }
        };

        UpdateText();
        UpdateButtons();
    }

    public decimal Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public decimal Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public decimal Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    public bool IsInputValid
    {
        get => GetValue(IsInputValidProperty);
        private set => SetValue(IsInputValidProperty, value);
    }

    public TextBox Input => _input;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if ((change.Property == ValueProperty && !_updatingValueFromText && !_settingInvalidValue) ||
            change.Property == FormatStringProperty)
            UpdateText();
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            if (!_settingInvalidValue) CoerceValue();
            UpdateButtons();
        }
    }

    private Border StepButton(string iconKind, string tooltip)
    {
        var icon = new HomisIcon
        {
            Kind = iconKind,
            Width = 12,
            Height = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        icon.Bind(HomisIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var button = new Border
        {
            MinHeight = 0,
            Child = icon
        };
        button.Classes.Add("number-field-step");
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            Step(Increment);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            Step(-Increment);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            UpdateText();
            e.Handled = true;
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        var current = _input.Text ?? string.Empty;
        var selectionStart = Math.Min(_input.SelectionStart, _input.SelectionEnd);
        var selectionLength = Math.Abs(_input.SelectionEnd - _input.SelectionStart);
        var proposed = current.Remove(selectionStart, selectionLength).Insert(selectionStart, e.Text);
        if (TryParseInput(proposed, out _)) return;

        e.Handled = true;
        InvalidateInput();
    }

    private void OnInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _updatingText) return;
        if (!TryParseInput(_input.Text, out var value))
        {
            InvalidateInput();
            return;
        }

        IsInputValid = true;
        _updatingValueFromText = true;
        SetCurrentValue(ValueProperty, value);
        _updatingValueFromText = false;
    }

    private void Step(decimal amount)
    {
        CommitText();
        decimal next;
        try
        {
            next = checked(Value + amount);
        }
        catch (OverflowException)
        {
            next = amount >= 0 ? Maximum : Minimum;
        }
        IsInputValid = true;
        SetCurrentValue(ValueProperty, Clamp(next));
        _input.Focus();
        _input.SelectAll();
    }

    private void CommitText()
    {
        if (_updatingText) return;
        if (TryParseInput(_input.Text, out var value))
        {
            IsInputValid = true;
            SetCurrentValue(ValueProperty, value);
            UpdateText();
            return;
        }

        InvalidateInput();
    }

    private void CoerceValue()
    {
        var coerced = Clamp(Value);
        if (coerced != Value)
            SetCurrentValue(ValueProperty, coerced);
    }

    private decimal Clamp(decimal value)
    {
        var minimum = Math.Min(Minimum, Maximum);
        var maximum = Math.Max(Minimum, Maximum);
        return Math.Clamp(value, minimum, maximum);
    }

    private bool TryParseInput(string? text, out decimal value)
    {
        value = 0;
        var input = text?.Trim() ?? string.Empty;
        if (input.Length == 0 || input.Any(character => char.IsLetter(character) || character is '+' or '-'))
            return false;

        var decimalPlaces = DecimalPlaces();
        var currentSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        var normalized = currentSeparator == "." ? input : input.Replace(currentSeparator, ".");
        if (normalized.Any(character => !char.IsDigit(character) && character != '.')) return false;
        if (normalized.Count(character => character == '.') > (decimalPlaces > 0 ? 1 : 0)) return false;

        var separatorIndex = normalized.IndexOf('.');
        if (separatorIndex >= 0 && normalized.Length - separatorIndex - 1 > decimalPlaces) return false;
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
            return false;

        return value >= Math.Min(Minimum, Maximum) && value <= Math.Max(Minimum, Maximum);
    }

    private int DecimalPlaces()
    {
        var separatorIndex = FormatString.IndexOf('.');
        return separatorIndex < 0 ? 0 : FormatString.Length - separatorIndex - 1;
    }

    private void InvalidateInput()
    {
        var shouldNotify = IsInputValid;
        IsInputValid = false;
        _settingInvalidValue = true;
        SetCurrentValue(ValueProperty, InvalidValue());
        _settingInvalidValue = false;

        _updatingText = true;
        _input.Text = string.Empty;
        _updatingText = false;

        if (shouldNotify) InputValidationNotifier.Notify(this, ValidationMessage());
    }

    private decimal InvalidValue()
    {
        var minimum = Math.Min(Minimum, Maximum);
        return minimum > decimal.MinValue ? minimum - 1 : decimal.MinValue;
    }

    private string ValidationMessage()
    {
        var minimum = Math.Min(Minimum, Maximum);
        var kind = DecimalPlaces() == 0 ? "whole number" : $"number with up to {DecimalPlaces()} decimal places";
        return minimum == 1
            ? $"Enter a {kind} of at least 1. Letters, negative values, and zero are not allowed."
            : minimum == 0
                ? $"Enter a {kind} of zero or greater. Letters and negative values are not allowed."
                : $"Enter a {kind} between {minimum} and {Math.Max(Minimum, Maximum)}.";
    }

    private void UpdateText()
    {
        if (_input is null) return;
        _updatingText = true;
        _input.Text = Value.ToString(FormatString, CultureInfo.CurrentCulture);
        _updatingText = false;
        IsInputValid = true;
    }

    private void UpdateButtons()
    {
        if (_incrementButton is null || _decrementButton is null) return;
        _incrementButton.IsEnabled = Value < Maximum;
        _decrementButton.IsEnabled = Value > Minimum;
    }

    private void OnChildGotFocus() => Classes.Set("focused", true);

    private void OnChildLostFocus() =>
        Dispatcher.UIThread.Post(() => Classes.Set("focused", IsKeyboardFocusWithin));

    private static T At<T>(T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }
}
