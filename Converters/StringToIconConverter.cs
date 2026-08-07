using System.Globalization;
using Avalonia.Data.Converters;
using Lucide.Avalonia;

namespace AvaloniaApp.Converters;

public class StringToIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string icon && Enum.TryParse<LucideIconKind>(icon, out var kind))
            return kind;
        return LucideIconKind.Box;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
