using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Views.Helper;

namespace AvaloniaApp.Views.UI;

public sealed class SegmentSwitch : Border
{
    private readonly List<Button> _buttons = [];
    private readonly IReadOnlyList<bool> _alerts;
    private readonly Action<int> _onSelected;
    private int _selectedIndex;

    public SegmentSwitch(IReadOnlyList<string> options, int selectedIndex, Action<int> onSelected,
        IReadOnlyList<bool>? alerts = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(selectedIndex, options.Count);
        if (alerts is not null && alerts.Count != options.Count)
            throw new ArgumentException("Alert count must match option count.", nameof(alerts));

        _selectedIndex = selectedIndex;
        _onSelected = onSelected;
        _alerts = alerts ?? Enumerable.Repeat(false, options.Count).ToArray();

        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(4);

        var panel = new UniformGrid { Columns = options.Count, ColumnSpacing = 4 };
        for (var index = 0; index < options.Count; index++)
        {
            var optionIndex = index;
            var button = new Button
            {
                Content = options[index],
                FontSize = 12,
                Padding = new Thickness(8, 7),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            button.Click += (_, _) => Select(optionIndex);
            _buttons.Add(button);
            panel.Children.Add(button);
        }

        Child = panel;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        ApplyTheme();
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _buttons.Count);
            _selectedIndex = value;
            ApplyTheme();
        }
    }

    private void Select(int index)
    {
        SelectedIndex = index;
        _onSelected(index);
    }

    private void ApplyTheme()
    {
        Background = AppTheme.Brush("Secondary", Brushes.WhiteSmoke);
        for (var index = 0; index < _buttons.Count; index++)
        {
            var active = index == SelectedIndex;
            var button = _buttons[index];
            button.Background = active ? AppTheme.Brush("Primary", Brushes.SlateBlue) : Brushes.Transparent;
            button.Foreground = active
                ? AppTheme.Brush("PrimaryForeground", Brushes.White)
                : _alerts[index]
                    ? AppTheme.Brush("DestructiveRed", Brushes.IndianRed)
                    : AppTheme.Brush("MutedForeground", Brushes.Gray);
            button.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }
}
