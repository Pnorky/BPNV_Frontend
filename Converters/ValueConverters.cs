using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaloniaApp.Converters;

public class BarHeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d && parameter is string s && double.TryParse(s, out double maxHeight))
        {
            double maxValue = 52;
            return (d / maxValue) * maxHeight;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
