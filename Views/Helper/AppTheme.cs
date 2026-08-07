using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace AvaloniaApp.Views.Helper;

public static class AppTheme
{
    public static T Resource<T>(string key, T fallback, ThemeVariant? themeVariant = null)
    {
        var application = Application.Current;
        return application?.TryGetResource(key, themeVariant ?? application.ActualThemeVariant, out var resource) == true && resource is T value
            ? value
            : fallback;
    }

    public static IBrush Brush(string key, IBrush fallback, ThemeVariant? themeVariant = null) =>
        Resource(key, fallback, themeVariant);

    public static Color Color(string key, Color fallback, ThemeVariant? themeVariant = null) =>
        Resource<ISolidColorBrush?>(key, null, themeVariant)?.Color ?? fallback;
}
