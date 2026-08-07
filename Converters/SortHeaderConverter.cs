using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace AvaloniaApp.Converters;

public sealed class SortHeaderConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string ?? "").Split('|', 2);
        var propertyName = parts[0];
        var label = parts.Length > 1 ? parts[1] : SplitName(propertyName);
        var summary = value as string ?? "";
        var match = Regex.Match(summary, $@"(?:Sorted by |, ){Regex.Escape(SplitName(propertyName))} ([↑↓]) (\d+)(?:,|$)");
        return match.Success ? $"{label} {match.Groups[1].Value} {match.Groups[2].Value}" : label;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static string SplitName(string value) => Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}
