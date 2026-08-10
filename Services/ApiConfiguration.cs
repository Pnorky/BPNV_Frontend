namespace AvaloniaApp.Services;

public static class ApiConfiguration
{
    public const string BaseUrlEnvironmentVariable = "BPNV_API_BASE_URL";

    public static Uri GetBaseAddress()
    {
        var value = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value)) value = "https://localhost:7282/";
        if (!value.EndsWith('/')) value += "/";

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"{BaseUrlEnvironmentVariable} must be an absolute HTTP or HTTPS URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback)
        {
            throw new InvalidOperationException(
                $"{BaseUrlEnvironmentVariable} must use HTTPS for non-localhost connections.");
        }

        return uri;
    }
}
