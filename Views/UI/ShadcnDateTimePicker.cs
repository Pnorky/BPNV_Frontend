using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using Lucide.Avalonia;

namespace AvaloniaApp.Views.UI;

public sealed class ShadcnDateTimePicker : Grid
{
    public static readonly StyledProperty<DateTimeOffset?> SelectedDateProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, DateTimeOffset?>(
            nameof(SelectedDate), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<TimeSpan?> SelectedTimeProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, TimeSpan?>(
            nameof(SelectedTime), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string> DateLabelProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, string>(nameof(DateLabel), "DATE");

    public static readonly StyledProperty<string> TimeLabelProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, string>(nameof(TimeLabel), "TIME");

    public static readonly StyledProperty<string> DatePlaceholderProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, string>(nameof(DatePlaceholder), "Select date");

    public static readonly StyledProperty<bool> ShowDateLabelProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, bool>(nameof(ShowDateLabel), true);

    public static readonly StyledProperty<string> TimePlaceholderProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, string>(nameof(TimePlaceholder), "12:30 PM");

    public static readonly StyledProperty<bool> ShowTimeProperty =
        AvaloniaProperty.Register<ShadcnDateTimePicker, bool>(nameof(ShowTime), true);

    private readonly TextBlock _dateLabel;
    private readonly TextBlock _timeLabel;
    private readonly TextBlock _dateDisplay;
    private readonly Button _dateTrigger;
    private readonly TextBox _timeInput;
    private readonly TextBlock _timeError;
    private readonly StackPanel _timeField;
    private readonly Popup _popup;
    private readonly Border _popupCard;
    private readonly TextBlock _monthTitle;
    private readonly UniformGrid _days;
    private readonly List<(Button Button, DateTimeOffset Date, bool OutsideMonth)> _dayButtons = [];
    private DateTime _displayMonth = new(StoreDateTime.StoreToday.Year, StoreDateTime.StoreToday.Month, 1);
    private Button? _selectedDayButton;
    private bool _suppressTimeText;
    private bool _updatingSelectedTime;
    private bool _isStacked;

    public ShadcnDateTimePicker()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        ColumnDefinitions = new ColumnDefinitions("292,156,*");
        RowDefinitions = new RowDefinitions("Auto");
        ColumnSpacing = 12;

        _dateLabel = FieldLabel(DateLabel);
        _timeLabel = FieldLabel(TimeLabel);

        var calendarIcon = new LucideIcon
        {
            Kind = LucideIconKind.CalendarDays,
            Width = 17,
            Height = 17,
            VerticalAlignment = VerticalAlignment.Center
        };
        calendarIcon.Bind(LucideIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));

        _dateDisplay = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var chevron = new LucideIcon
        {
            Kind = LucideIconKind.ChevronDown,
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.Bind(LucideIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));

        _dateTrigger = new Button
        {
            MinHeight = 42,
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(7),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 9,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children = { calendarIcon, At(_dateDisplay, 1), At(chevron, 2) }
            }
        };
        _dateTrigger.Classes.Add("secondary");
        AutomationProperties.SetName(_dateTrigger, DateLabel);
        _dateTrigger.Click += (_, _) => TogglePopup();
        _dateTrigger.KeyDown += OnDateTriggerKeyDown;

        var dateField = new StackPanel
        {
            Spacing = 7,
            Children = { _dateLabel, _dateTrigger }
        };

        _timeInput = new TextBox
        {
            MinHeight = 42,
            MaxLength = 8,
            PlaceholderText = TimePlaceholder,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _timeInput.Classes.Add("form-input");
        AutomationProperties.SetName(_timeInput, TimeLabel);
        _timeInput.TextChanged += OnTimeTextChanged;
        _timeInput.LostFocus += (_, _) => NormalizeTimeText();
        _timeInput.KeyDown += OnTimeKeyDown;

        _timeError = new TextBlock
        {
            Text = "Enter a valid time, such as 9:05 AM or 21:05.",
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        _timeError.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("Destructive"));

        _timeField = new StackPanel
        {
            Spacing = 7,
            Children = { _timeLabel, _timeInput, _timeError }
        };
        Grid.SetColumn(_timeField, 1);

        var previous = NavigationButton(LucideIconKind.ChevronLeft, "Previous month");
        previous.Click += (_, _) => MoveMonths(-1);
        var next = NavigationButton(LucideIconKind.ChevronRight, "Next month");
        next.Click += (_, _) => MoveMonths(1);
        Grid.SetColumn(next, 2);

        _monthTitle = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_monthTitle, 1);

        var calendarHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Children = { previous, _monthTitle, next }
        };

        var weekdays = new UniformGrid { Columns = 7 };
        foreach (var name in CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames)
        {
            var weekday = new TextBlock
            {
                Text = name[..Math.Min(2, name.Length)],
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2)
            };
            weekday.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
            weekdays.Children.Add(weekday);
        }

        _days = new UniformGrid { Columns = 7, Rows = 6 };

        var clear = new ActionButton("Clear", ActionButtonVariant.Ghost, ActionButtonSize.Sm);
        clear.Click += (_, _) => ClearDate();
        var today = new ActionButton("Today", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        today.Click += (_, _) => SelectDate(StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday));
        Grid.SetColumn(today, 2);
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Children = { clear, today }
        };

        _popupCard = new Border
        {
            Width = 270,
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetY = 5,
                Blur = 18,
                Color = Color.FromArgb(45, 0, 0, 0)
            }),
            Child = new StackPanel
            {
                Spacing = 10,
                Children = { calendarHeader, weekdays, _days, footer }
            }
        };
        _popupCard.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _popupCard.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _popupCard.KeyDown += OnPopupKeyDown;

        _popup = new Popup
        {
            PlacementTarget = _dateTrigger,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = _popupCard
        };

        Children.Add(dateField);
        Children.Add(_timeField);
        Children.Add(_popup);

        SizeChanged += (_, e) =>
        {
            UpdateLayoutMode(e.NewSize.Width);
            UpdatePopupWidth();
        };
        UpdateDateDisplay();
        UpdateTimeDisplay();
    }

    public DateTimeOffset? SelectedDate
    {
        get => GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public TimeSpan? SelectedTime
    {
        get => GetValue(SelectedTimeProperty);
        set => SetValue(SelectedTimeProperty, value);
    }

    public string DateLabel
    {
        get => GetValue(DateLabelProperty);
        set => SetValue(DateLabelProperty, value);
    }

    public string TimeLabel
    {
        get => GetValue(TimeLabelProperty);
        set => SetValue(TimeLabelProperty, value);
    }

    public string DatePlaceholder
    {
        get => GetValue(DatePlaceholderProperty);
        set => SetValue(DatePlaceholderProperty, value);
    }

    public bool ShowDateLabel
    {
        get => GetValue(ShowDateLabelProperty);
        set => SetValue(ShowDateLabelProperty, value);
    }

    public string TimePlaceholder
    {
        get => GetValue(TimePlaceholderProperty);
        set => SetValue(TimePlaceholderProperty, value);
    }

    public bool ShowTime
    {
        get => GetValue(ShowTimeProperty);
        set => SetValue(ShowTimeProperty, value);
    }

    internal static bool TryParseTime(string? text, CultureInfo culture, out TimeSpan? value) =>
        ShadcnTimePicker.TryParseTime(text, culture, out value);

    internal static string FormatTime(TimeSpan? value, CultureInfo culture) =>
        ShadcnTimePicker.FormatTime(value, culture);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedDateProperty)
        {
            if (SelectedDate.HasValue && !_popup.IsOpen)
                _displayMonth = new DateTime(SelectedDate.Value.Year, SelectedDate.Value.Month, 1);
            UpdateDateDisplay();
            if (_popup.IsOpen) BuildCalendar();
        }
        else if (change.Property == SelectedTimeProperty && !_updatingSelectedTime)
        {
            UpdateTimeDisplay();
        }
        else if (change.Property == DateLabelProperty)
        {
            _dateLabel.Text = DateLabel;
            AutomationProperties.SetName(_dateTrigger, DateLabel);
        }
        else if (change.Property == TimeLabelProperty)
        {
            _timeLabel.Text = TimeLabel;
            AutomationProperties.SetName(_timeInput, TimeLabel);
        }
        else if (change.Property == DatePlaceholderProperty)
        {
            UpdateDateDisplay();
        }
        else if (change.Property == ShowDateLabelProperty)
        {
            _dateLabel.IsVisible = ShowDateLabel;
        }
        else if (change.Property == TimePlaceholderProperty)
        {
            _timeInput.PlaceholderText = TimePlaceholder;
        }
        else if (change.Property == ShowTimeProperty)
        {
            _timeField.IsVisible = ShowTime;
            UpdateLayoutMode(Bounds.Width);
        }
        else if ((change.Property == IsEnabledProperty && !IsEnabled) ||
                 (change.Property == IsVisibleProperty && !IsVisible))
        {
            _popup.IsOpen = false;
        }
    }

    private void TogglePopup()
    {
        if (_popup.IsOpen)
        {
            ClosePopup(true);
            return;
        }

        if (!IsEnabled) return;
        if (SelectedDate.HasValue)
            _displayMonth = new DateTime(SelectedDate.Value.Year, SelectedDate.Value.Month, 1);
        BuildCalendar();
        _popup.IsOpen = true;
        (_selectedDayButton ?? _dayButtons.FirstOrDefault(item => !item.OutsideMonth).Button)?.Focus();
    }

    private void ClosePopup(bool restoreFocus)
    {
        _popup.IsOpen = false;
        if (restoreFocus) _dateTrigger.Focus();
    }

    private void MoveMonths(int offset)
    {
        _displayMonth = _displayMonth.AddMonths(offset);
        BuildCalendar();
    }

    private void BuildCalendar()
    {
        _monthTitle.Text = _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        _dayButtons.Clear();
        _days.Children.Clear();
        _selectedDayButton = null;

        var first = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
        var gridStart = first.AddDays(-(int)first.DayOfWeek);
        for (var index = 0; index < 42; index++)
        {
            var date = StoreDateTime.AtStoreMidnight(gridStart.AddDays(index));
            var outsideMonth = date.Month != _displayMonth.Month;
            var button = new Button
            {
                Content = date.Day.ToString(CultureInfo.CurrentCulture),
                Width = 34,
                Height = 34,
                MinHeight = 34,
                Padding = new Thickness(0),
                Margin = new Thickness(0.25),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                FontSize = 12,
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(button, date.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture));
            button.Click += (_, _) => SelectDate(date);
            _dayButtons.Add((button, date, outsideMonth));
            _days.Children.Add(button);
            if (SameDay(date, SelectedDate)) _selectedDayButton = button;
        }

        ApplyDayVisuals();
    }

    private void ApplyDayVisuals()
    {
        var today = StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
        foreach (var item in _dayButtons)
        {
            var selected = SameDay(item.Date, SelectedDate);
            item.Button.Opacity = item.OutsideMonth ? 0.42 : 1;
            item.Button.Background = Brushes.Transparent;
            item.Button.Foreground = GetResourceBrush("Foreground", Brushes.Black);
            item.Button.BorderBrush = SameDay(item.Date, today)
                ? GetResourceBrush("Primary", Brushes.DodgerBlue)
                : Brushes.Transparent;

            if (!selected) continue;
            item.Button.Background = GetResourceBrush("Primary", Brushes.DodgerBlue);
            item.Button.Foreground = GetResourceBrush("PrimaryForeground", Brushes.White);
            item.Button.BorderBrush = GetResourceBrush("Primary", Brushes.DodgerBlue);
            item.Button.Opacity = 1;
        }
    }

    private void SelectDate(DateTimeOffset date)
    {
        SetCurrentValue(SelectedDateProperty, StoreDateTime.AtStoreMidnight(date.Date));
        _displayMonth = new DateTime(date.Year, date.Month, 1);
        ClosePopup(true);
    }

    private void ClearDate()
    {
        SetCurrentValue(SelectedDateProperty, null);
        ClosePopup(true);
    }

    private void UpdateDateDisplay()
    {
        if (_dateDisplay is null) return;
        _dateDisplay.Text = SelectedDate.HasValue
            ? SelectedDate.Value.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture)
            : DatePlaceholder;
        _dateDisplay.Bind(TextBlock.ForegroundProperty,
            new DynamicResourceExtension(SelectedDate.HasValue ? "Foreground" : "MutedForeground"));
    }

    private void OnTimeTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressTimeText) return;

        var valid = TryParseTime(_timeInput.Text, CultureInfo.CurrentCulture, out var parsed);
        _updatingSelectedTime = true;
        SetCurrentValue(SelectedTimeProperty, valid ? parsed : null);
        _updatingSelectedTime = false;
        SetTimeInvalid(!valid);
    }

    private void NormalizeTimeText()
    {
        if (!TryParseTime(_timeInput.Text, CultureInfo.CurrentCulture, out var parsed))
        {
            SetTimeInvalid(true);
            return;
        }

        SetTimeInvalid(false);
        _suppressTimeText = true;
        _timeInput.Text = FormatTime(parsed, CultureInfo.CurrentCulture);
        _suppressTimeText = false;
    }

    private void UpdateTimeDisplay()
    {
        if (_timeInput is null) return;
        _suppressTimeText = true;
        _timeInput.Text = FormatTime(SelectedTime, CultureInfo.CurrentCulture);
        _suppressTimeText = false;
        SetTimeInvalid(false);
    }

    private void SetTimeInvalid(bool invalid)
    {
        if (invalid && !_timeInput.Classes.Contains("invalid"))
            _timeInput.Classes.Add("invalid");
        else if (!invalid)
            _timeInput.Classes.Remove("invalid");

        _timeError.IsVisible = invalid;
        ToolTip.SetTip(_timeInput, invalid ? _timeError.Text : null);
    }

    private void OnTimeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        NormalizeTimeText();
        e.Handled = true;
    }

    private void OnDateTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            if (!_popup.IsOpen) TogglePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _popup.IsOpen)
        {
            ClosePopup(true);
            e.Handled = true;
        }
    }

    private void OnPopupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        ClosePopup(true);
        e.Handled = true;
    }

    private void UpdateLayoutMode(double width)
    {
        if (!ShowTime)
        {
            ColumnDefinitions = new ColumnDefinitions("*");
            RowDefinitions = new RowDefinitions("Auto");
            ColumnSpacing = 0;
            RowSpacing = 0;
            Grid.SetColumn(_timeField, 0);
            Grid.SetRow(_timeField, 0);
            return;
        }

        var stacked = width < 460;
        if (_isStacked == stacked) return;
        _isStacked = stacked;

        if (stacked)
        {
            ColumnDefinitions = new ColumnDefinitions("*");
            RowDefinitions = new RowDefinitions("Auto,Auto");
            ColumnSpacing = 0;
            RowSpacing = 12;
            Grid.SetColumn(_timeField, 0);
            Grid.SetRow(_timeField, 1);
        }
        else
        {
            ColumnDefinitions = new ColumnDefinitions("292,156,*");
            RowDefinitions = new RowDefinitions("Auto");
            ColumnSpacing = 12;
            RowSpacing = 0;
            Grid.SetColumn(_timeField, 1);
            Grid.SetRow(_timeField, 0);
        }
    }

    private void UpdatePopupWidth()
    {
        if (_dateTrigger.Bounds.Width > 0)
            _popupCard.Width = _dateTrigger.Bounds.Width;
    }

    private static TextBlock FieldLabel(string text)
    {
        var label = new TextBlock { Text = text };
        label.Classes.Add("form-label");
        return label;
    }

    private static Button NavigationButton(LucideIconKind icon, string tooltip)
    {
        var button = new Button
        {
            Width = 30,
            Height = 30,
            MinHeight = 30,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(6),
            Content = new LucideIcon
            {
                Kind = icon,
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = new TranslateTransform(-4, -4)
            },
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("secondary");
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private IBrush GetResourceBrush(string key, IBrush fallback) =>
        this.FindResource(key) as IBrush ?? fallback;

    private static bool SameDay(DateTimeOffset date, DateTimeOffset? other) =>
        other.HasValue && date.Date == other.Value.Date;

    private static T At<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
