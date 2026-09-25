namespace AvaloniaApp.Services;

public static class SalesReportCategories
{
    public const string Other = "Other";
    public static IReadOnlyList<string> Values { get; } =
    [
        "Drinks", "Crackers", "Chips", "Candies", "H/S", "Lubricants",
        "Alcohol", "Tube Ice", "Ice Cream", "B. Coffee", Other
    ];

    public static bool IsValid(string? value) =>
        Values.Contains(value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);

    public static string NormalizeOrOther(string? value) =>
        Values.FirstOrDefault(category => string.Equals(category, value?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Other;
}
