using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaApp.Views.Helper;

namespace AvaloniaApp.Views.UI;

public sealed record BreadcrumbItem(string Text, Action? Navigate = null);

public sealed class Breadcrumb : StackPanel
{
    private readonly List<Action<IBrush>> _applyMutedBrush = [];
    private TextBlock? _current;

    public Breadcrumb()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        Spacing = 6;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
    }

    public void SetItems(params BreadcrumbItem[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Length == 0) throw new ArgumentException("A breadcrumb requires at least one item.", nameof(items));
        Children.Clear(); _applyMutedBrush.Clear(); _current = null;
        for (var index = 0; index < items.Length; index++)
        {
            if (index > 0)
            {
                var separator = new TextBlock { Text = ">", FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                _applyMutedBrush.Add(brush => separator.Foreground = brush); Children.Add(separator);
            }
            var item = items[index];
            var isCurrent = index == items.Length - 1;
            if (!isCurrent && item.Navigate is not null)
            {
                var button = new Button
                {
                    Content = item.Text, FontSize = 12, Padding = new Thickness(0), MinHeight = 0,
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Cursor = new Cursor(StandardCursorType.Hand)
                };
                button.Click += (_, _) => item.Navigate();
                _applyMutedBrush.Add(brush => button.Foreground = brush); Children.Add(button); continue;
            }
            var label = new TextBlock
            {
                Text = item.Text, FontSize = 14, FontWeight = isCurrent ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (isCurrent) _current = label; else _applyMutedBrush.Add(brush => label.Foreground = brush);
            Children.Add(label);
        }
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var muted = AppTheme.Brush("MutedForeground", Brushes.Gray);
        foreach (var applyBrush in _applyMutedBrush) applyBrush(muted);
        if (_current is not null) _current.Foreground = AppTheme.Brush("Foreground", Brushes.Black);
    }
}
