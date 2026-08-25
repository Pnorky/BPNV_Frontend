using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using AvaloniaApp.Services;
using Lucide.Avalonia;

namespace AvaloniaApp.Views.UI;

public sealed class DateRangePicker : Grid
{
    public static readonly StyledProperty<DateTimeOffset?> StartDateProperty =
        AvaloniaProperty.Register<DateRangePicker, DateTimeOffset?>(nameof(StartDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<DateTimeOffset?> EndDateProperty =
        AvaloniaProperty.Register<DateRangePicker, DateTimeOffset?>(nameof(EndDate), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> PlaceholderTextProperty =
        AvaloniaProperty.Register<DateRangePicker, string>(nameof(PlaceholderText), "Pick a date range");

    private readonly Border _anchor;
    private readonly TextBlock _display;
    private readonly Popup _popup;
    private readonly Border _popupCard;
    private readonly Grid _months;
    private readonly List<(Button Button, DateTimeOffset Date, bool OutsideMonth)> _dayButtons = [];
    private DateTime _displayMonth = new(StoreDateTime.StoreToday.Year, StoreDateTime.StoreToday.Month, 1);
    private DateTimeOffset? _hoverDate;

    public DateRangePicker()
    {
        MinHeight = 42;
        Focusable = true;

        var calendarIcon = new LucideIcon { Kind = LucideIconKind.CalendarDays, Width = 17, Height = 17 };
        calendarIcon.Bind(LucideIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        _display = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var chevron = new LucideIcon
        {
            Kind = LucideIconKind.ChevronDown,
            Width = 14,
            Height = 14,
            Margin = new Thickness(0, 0, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.Bind(LucideIcon.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
        var triggerContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 9,
            Children = { calendarIcon, At(_display, 1), At(chevron, 2) }
        };
        _anchor = new Border
        {
            MinHeight = 42,
            Padding = new Thickness(12, 8),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = triggerContent
        };
        _anchor.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _anchor.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _anchor.PointerPressed += (_, e) => { e.Handled = true; TogglePopup(); };

        var previous = NavigationButton(LucideIconKind.ChevronLeft, "Previous month");
        previous.Click += (_, _) => MoveMonths(-1);
        var next = NavigationButton(LucideIconKind.ChevronRight, "Next month");
        next.Click += (_, _) => MoveMonths(1);
        _months = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 6 };
        var calendarArea = new Grid
        {
            Children = { _months, previous, next }
        };
        previous.VerticalAlignment = VerticalAlignment.Top;
        previous.HorizontalAlignment = HorizontalAlignment.Left;
        previous.Margin = new Thickness(4, 0, 0, 0);
        next.VerticalAlignment = VerticalAlignment.Top;
        next.HorizontalAlignment = HorizontalAlignment.Right;
        next.Margin = new Thickness(0, 0, 4, 0);

        var today = new ActionButton("Today", ActionButtonVariant.Secondary, ActionButtonSize.Sm);
        today.Click += (_, _) => SelectSingleDay(Today());
        var clear = new ActionButton("Clear", ActionButtonVariant.Ghost, ActionButtonSize.Sm);
        clear.Click += (_, _) => Clear();
        var footer = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 6,
            Children = { At(clear, 1), At(today, 2) }
        };
        _popupCard = new Border
        {
            Width = 490,
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 5, Blur = 18, Color = Color.FromArgb(45, 0, 0, 0) }),
            Child = new StackPanel { Spacing = 10, Children = { calendarArea, footer } }
        };
        _popupCard.Bind(Border.BackgroundProperty, new DynamicResourceExtension("Card"));
        _popupCard.Bind(Border.BorderBrushProperty, new DynamicResourceExtension("Border"));
        _popup = new Popup
        {
            PlacementTarget = _anchor,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = true,
            Child = _popupCard
        };

        Children.Add(_anchor);
        Children.Add(_popup);
        KeyDown += OnKeyDown;
        UpdateDisplay();
        BuildMonths();
    }

    public DateTimeOffset? StartDate { get => GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DateTimeOffset? EndDate { get => GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }
    public string PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

    public void SelectDate(DateTimeOffset date)
    {
        var range = UpdateRange(StartDate, EndDate, date);
        SetCurrentValue(StartDateProperty, range.Start);
        SetCurrentValue(EndDateProperty, range.End);
        _hoverDate = null;
        UpdateDisplay();
        ApplyDayVisuals();
    }

    public void Clear()
    {
        SetCurrentValue(StartDateProperty, null);
        SetCurrentValue(EndDateProperty, null);
        _hoverDate = null;
        UpdateDisplay();
        ApplyDayVisuals();
    }

    public static string FormatRange(DateTimeOffset? start, DateTimeOffset? end, string placeholder = "Pick a date range")
    {
        if (!start.HasValue) return placeholder;
        var first = start.Value.ToString("MMMM d, yyyy", CultureInfo.CurrentCulture);
        return end.HasValue ? $"{first} - {end.Value:MMMM d, yyyy}" : first;
    }

    public static (DateTimeOffset? Start, DateTimeOffset? End) UpdateRange(
        DateTimeOffset? start,
        DateTimeOffset? end,
        DateTimeOffset selectedDate)
    {
        var selected = StoreDateTime.AtStoreMidnight(selectedDate.Date);
        if (!start.HasValue || end.HasValue) return (selected, null);
        return selected < start.Value ? (selected, start) : (start, selected);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StartDateProperty || change.Property == EndDateProperty || change.Property == PlaceholderTextProperty)
        {
            if (change.Property == StartDateProperty && StartDate.HasValue && !_popup.IsOpen)
                _displayMonth = new DateTime(StartDate.Value.Year, StartDate.Value.Month, 1);
            UpdateDisplay();
            BuildMonths();
        }
    }

    private void TogglePopup()
    {
        if (_popup.IsOpen) { _popup.IsOpen = false; return; }
        if (StartDate.HasValue) _displayMonth = new DateTime(StartDate.Value.Year, StartDate.Value.Month, 1);
        BuildMonths();
        _popup.IsOpen = true;
    }

    private void MoveMonths(int offset)
    {
        _displayMonth = _displayMonth.AddMonths(offset);
        BuildMonths();
    }

    private Control BuildMonth(DateTime month)
    {
        var title = new TextBlock
        {
            Text = month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            FontWeight = FontWeight.SemiBold,
            Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var weekdays = new UniformGrid { Columns = 7 };
        foreach (var label in CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames)
        {
            var day = new TextBlock
            {
                Text = label[..Math.Min(2, label.Length)],
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2)
            };
            day.Bind(TextBlock.ForegroundProperty, new DynamicResourceExtension("MutedForeground"));
            weekdays.Children.Add(day);
        }

        var days = new UniformGrid { Columns = 7, Rows = 6 };
        var first = new DateTime(month.Year, month.Month, 1);
        var gridStart = first.AddDays(-(int)first.DayOfWeek);
        for (var index = 0; index < 42; index++)
        {
            var date = StoreDateTime.AtStoreMidnight(gridStart.AddDays(index));
            var outside = date.Month != month.Month;
            var button = new Button
            {
                Content = date.Day.ToString(CultureInfo.CurrentCulture),
                Width = 30,
                Height = 30,
                MinHeight = 30,
                Padding = new Thickness(0),
                Margin = new Thickness(0.25),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                FontSize = 12,
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Click += (_, _) => SelectDate(date);
            button.PointerEntered += (_, _) =>
            {
                if (StartDate.HasValue && !EndDate.HasValue && date >= StartDate.Value)
                {
                    _hoverDate = date;
                    ApplyDayVisuals();
                }
            };
            _dayButtons.Add((button, date, outside));
            days.Children.Add(button);
        }
        return new StackPanel { Width = 215, Children = { title, weekdays, days } };
    }

    private void BuildMonths()
    {
        if (_months is null) return;
        _dayButtons.Clear();
        _months.Children.Clear();
        _months.Children.Add(BuildMonth(_displayMonth));
        _months.Children.Add(At(BuildMonth(_displayMonth.AddMonths(1)), 1));
        ApplyDayVisuals();
    }

    private void ApplyDayVisuals()
    {
        if (_dayButtons.Count == 0) return;
        var effectiveEnd = EndDate ?? _hoverDate;
        foreach (var item in _dayButtons)
        {
            var selectedEdge = SameDay(item.Date, StartDate) || SameDay(item.Date, EndDate);
            var inRange = StartDate.HasValue && effectiveEnd.HasValue &&
                          item.Date.Date > StartDate.Value.Date && item.Date.Date < effectiveEnd.Value.Date;
            item.Button.Opacity = item.OutsideMonth ? 0.42 : 1;
            item.Button.Background = Brushes.Transparent;
            item.Button.Foreground = GetResourceBrush("Foreground", Brushes.Black);
            item.Button.BorderBrush = SameDay(item.Date, Today())
                ? GetResourceBrush("Primary", Brushes.DodgerBlue)
                : Brushes.Transparent;
            if (inRange)
            {
                item.Button.Background = GetResourceBrush("Secondary", Brushes.LightGray);
                item.Button.Opacity = item.OutsideMonth ? 0.65 : 1;
            }
            if (selectedEdge || SameDay(item.Date, StartDate) && !EndDate.HasValue)
            {
                item.Button.Background = GetResourceBrush("Primary", Brushes.DodgerBlue);
                item.Button.Foreground = GetResourceBrush("PrimaryForeground", Brushes.White);
                item.Button.Opacity = 1;
            }
        }
    }

    private void SelectSingleDay(DateTimeOffset date)
    {
        SetCurrentValue(StartDateProperty, date);
        SetCurrentValue(EndDateProperty, date);
        _displayMonth = new DateTime(date.Year, date.Month, 1);
        UpdateDisplay();
        BuildMonths();
    }

    private void UpdateDisplay()
    {
        if (_display is null) return;
        _display.Text = FormatRange(StartDate, EndDate, PlaceholderText);
        _display.Bind(TextBlock.ForegroundProperty,
            new DynamicResourceExtension(StartDate.HasValue ? "Foreground" : "MutedForeground"));
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space or Key.Down)
        {
            if (!_popup.IsOpen) TogglePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _popup.IsOpen)
        {
            _popup.IsOpen = false;
            e.Handled = true;
        }
    }

    private Button NavigationButton(LucideIconKind icon, string tooltip)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            MinHeight = 28,
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
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("secondary");
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private IBrush GetResourceBrush(string key, IBrush fallback) => this.FindResource(key) as IBrush ?? fallback;
    private static bool SameDay(DateTimeOffset date, DateTimeOffset? other) => other.HasValue && date.Date == other.Value.Date;
    private static DateTimeOffset Today() => StoreDateTime.AtStoreMidnight(StoreDateTime.StoreToday);
    private static T At<T>(T control, int column) where T : Control { Grid.SetColumn(control, column); return control; }
}
