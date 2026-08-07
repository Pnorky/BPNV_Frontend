using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;

namespace AvaloniaApp.Services;

public static partial class ThemeConverter
{
    private static readonly Dictionary<string, string> CssToAvalonia = new()
    {
        ["background"] = "Background", ["foreground"] = "Foreground",
        ["card"] = "Card", ["card-foreground"] = "CardForeground",
        ["popover"] = "Popover", ["popover-foreground"] = "PopoverForeground",
        ["primary"] = "Primary", ["primary-foreground"] = "PrimaryForeground",
        ["secondary"] = "Secondary", ["secondary-foreground"] = "SecondaryForeground",
        ["muted"] = "Muted", ["muted-foreground"] = "MutedForeground",
        ["accent"] = "Accent", ["accent-foreground"] = "AccentForeground",
        ["destructive"] = "Destructive", ["destructive-foreground"] = "DestructiveForeground",
        ["border"] = "Border", ["input"] = "Input", ["ring"] = "Ring",
        ["chart-1"] = "Chart1", ["chart-2"] = "Chart2", ["chart-3"] = "Chart3",
        ["chart-4"] = "Chart4", ["chart-5"] = "Chart5",
        ["sidebar"] = "Sidebar", ["sidebar-background"] = "Sidebar",
        ["sidebar-foreground"] = "SidebarForeground", ["sidebar-primary"] = "SidebarPrimary",
        ["sidebar-primary-foreground"] = "SidebarPrimaryForeground",
        ["sidebar-accent"] = "SidebarAccent", ["sidebar-accent-foreground"] = "SidebarAccentForeground",
        ["sidebar-border"] = "SidebarBorder", ["sidebar-ring"] = "SidebarRing",
        ["sidebar-header"] = "SidebarHeader", ["nav-hover"] = "NavHover", ["nav-selected"] = "NavSelected",
        ["state-hover"] = "Hover", ["state-pressed"] = "Pressed", ["state-selected"] = "Selected",
        ["state-focus"] = "Focus", ["state-disabled"] = "Disabled",
        ["success"] = "SuccessGreen", ["success-foreground"] = "SuccessForeground",
        ["warning"] = "WarningYellow", ["warning-foreground"] = "WarningForeground",
        ["pending"] = "Pending", ["pending-foreground"] = "PendingForeground",
        ["information"] = "Information", ["information-foreground"] = "InformationForeground"
    };

    private static readonly Dictionary<string, string> ShadowResources = new()
    {
        ["shadow-2xs"] = "Shadow2Xs", ["shadow-xs"] = "ShadowXs", ["shadow-sm"] = "ShadowSm",
        ["shadow"] = "Shadow", ["shadow-md"] = "ShadowMd", ["shadow-lg"] = "ShadowLg",
        ["shadow-xl"] = "ShadowXl", ["shadow-2xl"] = "Shadow2Xl"
    };

    public static void ApplyCssAsset(Application application, Uri assetUri)
    {
        try
        {
            using var stream = AssetLoader.Open(assetUri);
            using var reader = new StreamReader(stream);
            ApplyCss(application, reader.ReadToEnd());
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Unable to apply CSS theme '{assetUri}': {exception}");
        }
    }

    public static void ApplyCss(Application application, string css)
    {
        if (application.Resources is not ResourceDictionary resources) return;
        var theme = ParseTheme(css);
        ApplyVariant(resources, ThemeVariant.Light, theme.Light);

        var dark = new Dictionary<string, string>(theme.Light, StringComparer.OrdinalIgnoreCase);
        foreach (var variable in theme.Dark) dark[variable.Key] = variable.Value;
        ApplyVariant(resources, ThemeVariant.Dark, ResolveVariables(dark));
    }

    public static CssTheme ParseTheme(string css)
    {
        var clean = CommentRegex().Replace(css, "");
        return new CssTheme(
            ResolveVariables(ParseCssVariables(ExtractBlock(clean, @":root"))),
            ParseCssVariables(ExtractBlock(clean, @"\.dark")));
    }

    public static Dictionary<string, string> ParseCssVariables(string cssBlock)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in VariableRegex().Matches(cssBlock))
            result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
        return result;
    }

    public static string ConvertCssToXaml(string css, string themeKey = "Light")
    {
        var theme = ParseTheme(css);
        var variables = theme.Light;
        if (themeKey.Equals("Dark", StringComparison.OrdinalIgnoreCase))
        {
            variables = new Dictionary<string, string>(theme.Light, StringComparer.OrdinalIgnoreCase);
            foreach (var variable in theme.Dark) variables[variable.Key] = variable.Value;
            variables = ResolveVariables(variables);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"        <ResourceDictionary x:Key=\"{themeKey}\">");
        if (variables.TryGetValue("primary", out var primary))
            sb.AppendLine($"          <Color x:Key=\"SystemAccentColor\">{ToColor(primary)}</Color>");
        foreach (var mapping in CssToAvalonia)
            if (variables.TryGetValue(mapping.Key, out var value))
                sb.AppendLine($"          <SolidColorBrush x:Key=\"{mapping.Value}\" Color=\"{ToColor(value)}\" />");
        sb.Append("        </ResourceDictionary>");
        return sb.ToString();
    }

    public static string OklchToHex(string value) => ToColor(value).ToString();

    private static void ApplyVariant(ResourceDictionary root, ThemeVariant variant,
        IReadOnlyDictionary<string, string> variables)
    {
        if (!root.ThemeDictionaries.TryGetValue(variant, out var provider) || provider is not ResourceDictionary dictionary)
        {
            dictionary = new ResourceDictionary();
            root.ThemeDictionaries[variant] = dictionary;
        }

        foreach (var mapping in CssToAvalonia)
            if (variables.TryGetValue(mapping.Key, out var value))
                dictionary[mapping.Value] = new SolidColorBrush(ToColor(value));

        if (variables.TryGetValue("primary", out var primary)) dictionary["SystemAccentColor"] = ToColor(primary);
        if (variables.TryGetValue("font-sans", out var sans)) dictionary["FontSans"] = ParseFontFamily(sans);
        if (variables.TryGetValue("font-serif", out var serif)) dictionary["FontSerif"] = ParseFontFamily(serif);
        if (variables.TryGetValue("font-mono", out var mono)) dictionary["FontMono"] = ParseFontFamily(mono);

        ApplyDerivedColors(dictionary, variables);
        ApplyRadii(dictionary, variables);
        ApplyShadows(dictionary, variables);
        ApplyOpacityAndMotion(dictionary, variables);
    }

    private static void ApplyDerivedColors(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> variables)
    {
        var foreground = BrushColor(dictionary, "Foreground", Colors.Black);
        var card = BrushColor(dictionary, "Card", Colors.White);
        var sidebar = BrushColor(dictionary, "Sidebar", card);
        var primary = BrushColor(dictionary, "Primary", Colors.SteelBlue);
        var primaryForeground = BrushColor(dictionary, "PrimaryForeground", Colors.White);
        var ring = BrushColor(dictionary, "Ring", primary);
        var muted = BrushColor(dictionary, "Muted", card);
        var destructive = BrushColor(dictionary, "Destructive", Color.Parse("#EF4444"));

        EnsureBrush(dictionary, variables, "Hover", "state-hover", Blend(card, foreground, .06));
        EnsureBrush(dictionary, variables, "Pressed", "state-pressed", Blend(card, foreground, .12));
        EnsureBrush(dictionary, variables, "Selected", "state-selected", Blend(card, primary, .16));
        EnsureBrush(dictionary, variables, "Focus", "state-focus", ring);
        EnsureBrush(dictionary, variables, "Disabled", "state-disabled", muted);
        EnsureBrush(dictionary, variables, "NavHover", "nav-hover", Blend(sidebar, foreground, .06));
        EnsureBrush(dictionary, variables, "NavSelected", "nav-selected", Blend(sidebar, primary, .18));
        EnsureBrush(dictionary, variables, "SidebarHeader", "sidebar-header", Blend(sidebar, foreground, .035));
        EnsureBrush(dictionary, variables, "ErrorBorder", "error-border", destructive);

        EnsureBrush(dictionary, variables, "SuccessGreen", "success", Color.Parse("#22C55E"));
        EnsureBrush(dictionary, variables, "SuccessForeground", "success-foreground", Colors.White);
        EnsureBrush(dictionary, variables, "WarningYellow", "warning", Color.Parse("#EAB308"));
        EnsureBrush(dictionary, variables, "WarningForeground", "warning-foreground", Colors.Black);
        EnsureBrush(dictionary, variables, "Pending", "pending", BrushColor(dictionary, "WarningYellow", Color.Parse("#EAB308")));
        EnsureBrush(dictionary, variables, "PendingForeground", "pending-foreground", BrushColor(dictionary, "WarningForeground", Colors.Black));
        EnsureBrush(dictionary, variables, "Information", "information", Color.Parse("#3B82F6"));
        EnsureBrush(dictionary, variables, "InformationForeground", "information-foreground", Colors.White);
        dictionary["DestructiveRed"] = new SolidColorBrush(destructive);
        dictionary["GrayBlue"] = new SolidColorBrush(BrushColor(dictionary, "MutedForeground", Color.Parse("#6B7280")));

        // Keep Fluent control focus and selection states aligned with the CSS theme.
        dictionary["SystemControlBackgroundAccentBrush"] = new SolidColorBrush(primary);
        dictionary["SystemControlForegroundAccentBrush"] = new SolidColorBrush(primaryForeground);
        dictionary["SystemControlHighlightAccentBrush"] = new SolidColorBrush(primary);
        dictionary["SystemControlHighlightAltAccentBrush"] = new SolidColorBrush(primary);
        dictionary["SystemControlRevealFocusVisualBrush"] = new SolidColorBrush(ring);
        dictionary["SystemControlFocusVisualPrimaryBrush"] = new SolidColorBrush(ring);
        dictionary["SystemControlFocusVisualSecondaryBrush"] = new SolidColorBrush(card);
        dictionary["SystemControlHyperlinkTextBrush"] = new SolidColorBrush(primary);
        dictionary["SystemControlHighlightListAccentHighBrush"] = new SolidColorBrush(primary) { Opacity = 0.7 };
        dictionary["SystemControlHighlightListAccentMediumBrush"] = new SolidColorBrush(primary) { Opacity = 0.6 };
        dictionary["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(primary) { Opacity = 0.4 };
        dictionary["SystemControlHighlightAltListAccentHighBrush"] = new SolidColorBrush(primary) { Opacity = 0.7 };
        dictionary["SystemControlHighlightAltListAccentMediumBrush"] = new SolidColorBrush(primary) { Opacity = 0.6 };
        dictionary["SystemControlHighlightAltListAccentLowBrush"] = new SolidColorBrush(primary) { Opacity = 0.4 };
        dictionary["TextControlBorderBrushFocused"] = new SolidColorBrush(ring);
        dictionary["TextControlSelectionHighlightColor"] = new SolidColorBrush(primary);
    }

    private static void ApplyRadii(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> variables)
    {
        var radius = ParseCssLength(Get(variables, "radius", "0.375rem"));
        dictionary["Radius"] = new CornerRadius(radius);
        dictionary["RadiusSm"] = new CornerRadius(ParseCssLength(Get(variables, "radius-sm", $"{Math.Max(0, radius - 4)}px")));
        dictionary["RadiusMd"] = new CornerRadius(ParseCssLength(Get(variables, "radius-md", $"{Math.Max(0, radius - 2)}px")));
        dictionary["RadiusLg"] = new CornerRadius(ParseCssLength(Get(variables, "radius-lg", $"{radius}px")));
        dictionary["RadiusXl"] = new CornerRadius(ParseCssLength(Get(variables, "radius-xl", $"{radius + 4}px")));
    }

    private static void ApplyShadows(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> variables)
    {
        foreach (var mapping in ShadowResources)
            if (variables.TryGetValue(mapping.Key, out var value))
                dictionary[mapping.Value] = ParseBoxShadows(value);
    }

    private static void ApplyOpacityAndMotion(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> variables)
    {
        dictionary["DisabledOpacity"] = ParseAlpha(Get(variables, "opacity-disabled", "0.5"));
        dictionary["HoverOpacity"] = ParseAlpha(Get(variables, "opacity-hover", "0.9"));
        dictionary["PressedOpacity"] = ParseAlpha(Get(variables, "opacity-pressed", "0.8"));
        var duration = ParseDuration(Get(variables, "duration-fast", "120ms"));
        var easing = ParseEasing(Get(variables, "ease-standard", "cubic-bezier(0.2, 0, 0, 1)"));
        dictionary["DurationFast"] = duration;
        dictionary["EaseStandard"] = easing;
        dictionary["ThemeOpacityTransitions"] = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing }
        };
    }

    private static Dictionary<string, string> ResolveVariables(Dictionary<string, string> variables)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in variables.Keys)
            result[name] = ResolveValue(variables[name], variables, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return result;
    }

    private static string ResolveValue(string value, IReadOnlyDictionary<string, string> variables, HashSet<string> stack)
    {
        for (var pass = 0; pass < 20 && value.Contains("var(", StringComparison.OrdinalIgnoreCase); pass++)
        {
            if (!TryFindVar(value, out var start, out var length, out var name, out var fallback)) break;
            var replacement = fallback ?? "transparent";
            if (stack.Add(name) && variables.TryGetValue(name, out var referenced))
                replacement = ResolveValue(referenced, variables, stack);
            stack.Remove(name);
            value = value[..start] + replacement + value[(start + length)..];
        }
        return value.Trim();
    }

    private static bool TryFindVar(string value, out int start, out int length, out string name, out string? fallback)
    {
        start = value.IndexOf("var(", StringComparison.OrdinalIgnoreCase);
        length = 0;
        name = "";
        fallback = null;
        if (start < 0) return false;

        var depth = 1;
        var comma = -1;
        for (var index = start + 4; index < value.Length; index++)
        {
            if (value[index] == '(') depth++;
            else if (value[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    var nameEnd = comma >= 0 ? comma : index;
                    var rawName = value[(start + 4)..nameEnd].Trim();
                    if (!rawName.StartsWith("--", StringComparison.Ordinal)) return false;
                    name = rawName[2..];
                    fallback = comma >= 0 ? value[(comma + 1)..index].Trim() : null;
                    length = index - start + 1;
                    return true;
                }
            }
            else if (value[index] == ',' && depth == 1 && comma < 0) comma = index;
        }
        return false;
    }

    private static string ExtractBlock(string css, string selector)
    {
        var match = Regex.Match(css, $@"(?s){selector}\s*\{{(?<body>.*?)\}}");
        return match.Success ? match.Groups["body"].Value : "";
    }

    private static FontFamily ParseFontFamily(string value) => new(value.Split(',')[0].Trim().Trim('\'', '"'));

    private static double ParseCssLength(string value)
    {
        var match = CssLengthRegex().Match(value.Trim());
        if (!match.Success) return 0;
        var number = ParseNumber(match.Groups[1].Value, 0);
        return match.Groups[2].Value.Equals("rem", StringComparison.OrdinalIgnoreCase) ? number * 16 : number;
    }

    private static TimeSpan ParseDuration(string value)
    {
        var match = DurationRegex().Match(value.Trim());
        if (!match.Success) return TimeSpan.Zero;
        var amount = ParseNumber(match.Groups[1].Value, 0);
        return match.Groups[2].Value.Equals("s", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromSeconds(amount) : TimeSpan.FromMilliseconds(amount);
    }

    private static Easing ParseEasing(string value)
    {
        try { return Easing.Parse(value); }
        catch { return new LinearEasing(); }
    }

    private static BoxShadows ParseBoxShadows(string value)
    {
        value = CssColorFunctionRegex().Replace(value, match => ToColor(match.Value).ToString());
        value = CssLengthInTextRegex().Replace(value, match =>
            ParseCssLength(match.Value).ToString("0.###", CultureInfo.InvariantCulture));
        return BoxShadows.Parse(value);
    }

    private static Color ToColor(string cssValue)
    {
        var value = cssValue.Trim();
        if (value.Equals("transparent", StringComparison.OrdinalIgnoreCase)) return Colors.Transparent;
        if (value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)) return ParseRgb(value);
        if (value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase)) return ParseHsl(value);
        if (!value.StartsWith("oklch", StringComparison.OrdinalIgnoreCase)) return Color.Parse(value);

        var match = OklchRegex().Match(value);
        if (!match.Success) throw new FormatException($"Invalid OKLCH color: {value}");
        var lightness = ParseColorComponent(match.Groups[1].Value);
        var chroma = ParseNumber(match.Groups[2].Value, 0);
        var hue = ParseNumber(match.Groups[3].Value, 0) * Math.PI / 180.0;
        var alpha = match.Groups[4].Success ? ParseAlpha(match.Groups[4].Value) : 1.0;

        var a = chroma * Math.Cos(hue);
        var b = chroma * Math.Sin(hue);
        var lRoot = lightness + 0.3963377774 * a + 0.2158037573 * b;
        var mRoot = lightness - 0.1055613458 * a - 0.0638541728 * b;
        var sRoot = lightness - 0.0894841775 * a - 1.2914855480 * b;
        var l = lRoot * lRoot * lRoot;
        var m = mRoot * mRoot * mRoot;
        var s = sRoot * sRoot * sRoot;
        return FromChannels(alpha,
            +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s, true);
    }

    private static Color ParseRgb(string value)
    {
        var parts = ColorParts(value);
        if (parts.Count < 3) throw new FormatException($"Invalid RGB color: {value}");
        double Channel(string part) => part.EndsWith('%') ? ParseNumber(part[..^1], 0) / 100 : ParseNumber(part, 0) / 255;
        return FromChannels(parts.Count > 3 ? ParseAlpha(parts[3]) : 1,
            Channel(parts[0]), Channel(parts[1]), Channel(parts[2]), false);
    }

    private static Color ParseHsl(string value)
    {
        var parts = ColorParts(value);
        if (parts.Count < 3) throw new FormatException($"Invalid HSL color: {value}");
        var hue = ((ParseNumber(parts[0].Replace("deg", "", StringComparison.OrdinalIgnoreCase), 0) % 360) + 360) % 360 / 360;
        var saturation = ParseColorComponent(parts[1]);
        var lightness = ParseColorComponent(parts[2]);
        double Channel(double offset)
        {
            var k = (offset + hue * 12) % 12;
            return lightness - saturation * Math.Min(lightness, 1 - lightness) * Math.Max(-1, Math.Min(k - 3, Math.Min(9 - k, 1)));
        }
        return FromChannels(parts.Count > 3 ? ParseAlpha(parts[3]) : 1,
            Channel(0), Channel(8), Channel(4), false);
    }

    private static List<string> ColorParts(string value)
    {
        var body = value[(value.IndexOf('(') + 1)..value.LastIndexOf(')')];
        return body.Replace(',', ' ').Replace("/", " / ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part != "/").ToList();
    }

    private static Color FromChannels(double alpha, double red, double green, double blue, bool linear)
    {
        static byte Channel(double channel, bool convertLinear)
        {
            if (convertLinear)
                channel = channel <= 0.0031308 ? 12.92 * channel : 1.055 * Math.Pow(Math.Max(0, channel), 1.0 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(channel, 0, 1) * 255);
        }
        return Color.FromArgb((byte)Math.Round(Math.Clamp(alpha, 0, 1) * 255),
            Channel(red, linear), Channel(green, linear), Channel(blue, linear));
    }

    private static Color Blend(Color background, Color foreground, double amount) => Color.FromArgb(
        255,
        (byte)Math.Round(background.R + (foreground.R - background.R) * amount),
        (byte)Math.Round(background.G + (foreground.G - background.G) * amount),
        (byte)Math.Round(background.B + (foreground.B - background.B) * amount));

    private static void EnsureBrush(ResourceDictionary dictionary, IReadOnlyDictionary<string, string> variables,
        string resourceKey, string cssKey, Color fallback)
    {
        dictionary[resourceKey] = new SolidColorBrush(variables.TryGetValue(cssKey, out var value) ? ToColor(value) : fallback);
    }

    private static Color BrushColor(ResourceDictionary dictionary, string key, Color fallback) =>
        dictionary.TryGetValue(key, out var resource) && resource is ISolidColorBrush brush ? brush.Color : fallback;
    private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) ? value : fallback;
    private static double ParseColorComponent(string value) =>
        value.EndsWith('%') ? ParseNumber(value[..^1], 0) / 100 : ParseNumber(value, 0);
    private static double ParseAlpha(string value) => Math.Clamp(ParseColorComponent(value), 0, 1);
    private static double ParseNumber(string value, double fallback) =>
        double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;

    public sealed record CssTheme(Dictionary<string, string> Light, Dictionary<string, string> Dark);

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)] private static partial Regex CommentRegex();
    [GeneratedRegex(@"--([\w-]+)\s*:\s*([^;]+);", RegexOptions.Multiline)] private static partial Regex VariableRegex();
    [GeneratedRegex(@"^(-?[\d.]+)\s*(px|rem)?$", RegexOptions.IgnoreCase)] private static partial Regex CssLengthRegex();
    [GeneratedRegex(@"^([\d.]+)\s*(ms|s)$", RegexOptions.IgnoreCase)] private static partial Regex DurationRegex();
    [GeneratedRegex(@"(?:oklch|rgba?|hsla?)\([^\)]+\)", RegexOptions.IgnoreCase)] private static partial Regex CssColorFunctionRegex();
    [GeneratedRegex(@"-?[\d.]+(?:px|rem)", RegexOptions.IgnoreCase)] private static partial Regex CssLengthInTextRegex();
    [GeneratedRegex(@"oklch\(\s*([\d.]+%?)\s+([\d.]+)\s+(-?[\d.]+)(?:\s*/\s*([\d.]+%?))?\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex OklchRegex();
}
