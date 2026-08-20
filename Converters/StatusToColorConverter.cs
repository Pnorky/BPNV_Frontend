using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AvaloniaApp.Converters;

public class StatusToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        var resourceKey = status.ToLowerInvariant() switch
        {
            "admitted" or "active" or "in stock" or "paid" or "completed" or "scheduled" => "SuccessGreen",
            "outpatient" or "low stock" or "warning" or "partial" or "pending" => "Pending",
            "er" or "stat" or "out of stock" or "critical" or "inactive" or "delinquent" => "DestructiveRed",
            _ => "GrayBlue"
        };
        return FindBrush(resourceKey, "#6B7280");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static IBrush FindBrush(string key, string fallback)
    {
        var application = Application.Current;
        return application?.TryGetResource(key, application.ActualThemeVariant, out var resource) == true && resource is IBrush brush
            ? brush
            : new SolidColorBrush(Color.Parse(fallback));
    }
}

public class StatusToForegroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value?.ToString() ?? "";
        var resourceKey = status.ToLowerInvariant() switch
        {
            "admitted" or "active" or "in stock" or "paid" or "completed" or "scheduled" => "SuccessForeground",
            "outpatient" or "low stock" or "warning" or "partial" or "pending" => "PendingForeground",
            "er" or "stat" or "out of stock" or "critical" or "inactive" or "delinquent" => "DestructiveForeground",
            _ => "InformationForeground"
        };
        return FindBrush(resourceKey);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();

    private static IBrush FindBrush(string key)
    {
        var application = Application.Current;
        return application?.TryGetResource(key, application.ActualThemeVariant, out var resource) == true && resource is IBrush brush
            ? brush
            : Brushes.White;
    }
}
