using System.Net;

namespace AvaloniaApp.Services;

public static class UserFacingErrors
{
    public static string Get(Exception exception, string fallback = "Something went wrong. Please try again.") => exception switch
    {
        ApiClientException api when !string.IsNullOrWhiteSpace(api.Message) => api.Message,
        HttpRequestException => "Cannot connect to the store service. Check the connection and try again.",
        TaskCanceledException => "The request took too long to finish. Please try again.",
        InvalidOperationException => fallback,
        FormatException => "The entered information is not in a valid format.",
        OverflowException => "The entered number is too large.",
        _ => fallback
    };
}
