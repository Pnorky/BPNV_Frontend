using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;

namespace AvaloniaApp.Views.UI;

public sealed class ShadcnTimePicker : TextBox
{
    public static readonly StyledProperty<TimeSpan?> SelectedTimeProperty =
        AvaloniaProperty.Register<ShadcnTimePicker, TimeSpan?>(
            nameof(SelectedTime), defaultBindingMode: BindingMode.TwoWay, enableDataValidation: true);

    private const string ValidationMessage = "Enter a valid time, such as 9:05 AM or 21:05.";
    private bool _updatingText;
    private bool _updatingValue;

    protected override Type StyleKeyOverride => typeof(TextBox);

    public ShadcnTimePicker()
    {
        Classes.Add("form-input");
        MinHeight = 42;
        MaxLength = 8;
        PlaceholderText = "12:30 PM";
        VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        LostFocus += (_, _) => NormalizeText();
        KeyDown += OnKeyDown;
    }

    public TimeSpan? SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    internal static bool TryParseTime(string? text, CultureInfo culture, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;

        var input = text.Trim();
        string[] twelveHourFormats = ["h:mm tt", "hh:mm tt", "h:mmtt", "hh:mmtt"];
        if (DateTime.TryParseExact(input, twelveHourFormats, culture,
                DateTimeStyles.AllowWhiteSpaces, out var twelveHour))
        {
            value = new TimeSpan(twelveHour.Hour, twelveHour.Minute, 0);
            return true;
        }

        string[] twentyFourHourFormats = ["H:mm", "HH:mm"];
        if (DateTime.TryParseExact(input, twentyFourHourFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var twentyFourHour))
        {
            value = new TimeSpan(twentyFourHour.Hour, twentyFourHour.Minute, 0);
            return true;
        }

        return false;
    }

    internal static string FormatTime(TimeSpan? value, CultureInfo culture)
    {
        if (!value.HasValue || value.Value < TimeSpan.Zero || value.Value >= TimeSpan.FromDays(1))
            return string.Empty;

        return new DateTime(2000, 1, 1).Add(value.Value).ToString("h:mm tt", culture);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedTimeProperty && !_updatingValue)
            UpdateText();
        else if (change.Property == TextProperty && !_updatingText)
            UpdateValue();
    }

    private void UpdateValue()
    {
        var valid = TryParseTime(Text, CultureInfo.CurrentCulture, out var parsed);
        _updatingValue = true;
        SetCurrentValue(SelectedTimeProperty, valid ? parsed : null);
        _updatingValue = false;
        SetInvalid(!valid);
    }

    private void UpdateText()
    {
        _updatingText = true;
        Text = FormatTime(SelectedTime, CultureInfo.CurrentCulture);
        _updatingText = false;
        SetInvalid(false);
    }

    private void NormalizeText()
    {
        if (!TryParseTime(Text, CultureInfo.CurrentCulture, out var parsed))
        {
            SetInvalid(true);
            return;
        }

        SetInvalid(false);
        _updatingText = true;
        Text = FormatTime(parsed, CultureInfo.CurrentCulture);
        _updatingText = false;
    }

    private void SetInvalid(bool invalid)
    {
        Classes.Set("invalid", invalid);
        ToolTip.SetTip(this, invalid ? ValidationMessage : null);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NormalizeText();
        e.Handled = true;
    }
}
