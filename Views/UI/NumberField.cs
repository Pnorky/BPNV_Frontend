using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Threading;
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

    private readonly TextBox _input;
    private readonly Border _incrementButton;
    private readonly Border _decrementButton;
    private bool _updatingText;

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

    public TextBox Input => _input;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == FormatStringProperty)
            UpdateText();
        if (change.Property == ValueProperty || change.Property == MinimumProperty || change.Property == MaximumProperty)
        {
            CoerceValue();
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
        SetCurrentValue(ValueProperty, Clamp(next));
        _input.Focus();
        _input.SelectAll();
    }

    private void CommitText()
    {
        if (_updatingText) return;
        var text = _input.Text?.Trim();
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var value) ||
            decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            SetCurrentValue(ValueProperty, Clamp(value));
        }
        UpdateText();
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

    private void UpdateText()
    {
        if (_input is null) return;
        _updatingText = true;
        _input.Text = Value.ToString(FormatString, CultureInfo.CurrentCulture);
        _updatingText = false;
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
