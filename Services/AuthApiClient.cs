using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AvaloniaApp.Services;

internal sealed record LoginRequest(string Username, string Password);
internal sealed record RefreshTokenRequest(string RefreshToken);
internal sealed record LogoutRequest(string RefreshToken);
internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record TokenResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    AuthenticatedUser User);

internal sealed record ApiProblem(string? Title, string? Detail);

public sealed class ApiClientException(
    HttpStatusCode statusCode,
    string message,
    ApiProblemDetails? problem = null) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public ApiProblemDetails? Problem { get; } = problem;
}

public sealed class AuthApiClient(HttpClient httpClient, AuthSession session)
{
    // Admin is the highest role currently defined by the backend.
    public bool IsSuperAdmin => session.HasRole("SuperAdmin") || session.HasRole("Admin");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public Uri BaseAddress => httpClient.BaseAddress!;

    public async Task<AuthenticatedUser> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var revision = session.BeginAuthentication();
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(username, password),
            JsonOptions,
            cancellationToken);
        var tokens = await ReadTokenResponseAsync(response, cancellationToken);
        if (!session.TrySet(tokens, revision))
            throw new ApiClientException(HttpStatusCode.Unauthorized, "A newer authentication request replaced this login.");
        return tokens.User;
    }

    public async Task<AuthenticatedUser> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var revision = session.GetSnapshot().Revision;
        using var response = await SendAuthorizedAsync(
            () => CreateJsonRequest(
                HttpMethod.Post,
                "api/auth/change-password",
                new ChangePasswordRequest(currentPassword, newPassword)),
            cancellationToken);
        var tokens = await ReadTokenResponseAsync(response, cancellationToken);
        if (!session.TrySet(tokens, revision))
            throw new ApiClientException(HttpStatusCode.Unauthorized, "The session changed before the password update completed.");
        return tokens.User;
    }

    public async Task<HttpResponseMessage> SendAuthorizedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken = default)
    {
        var snapshot = session.GetSnapshot();
        if (snapshot.AccessToken is null || snapshot.User is null)
            throw new ApiClientException(HttpStatusCode.Unauthorized, "The session has ended. Sign in again.");

        if (snapshot.AccessTokenExpiresAtUtc <= DateTime.UtcNow.AddSeconds(30))
            await RefreshAsync(cancellationToken, force: false, rejectedAccessToken: null);

        var rejectedAccessToken = session.AccessToken;
        var response = await SendWithAccessTokenAsync(requestFactory, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        await RefreshAsync(cancellationToken, force: true, rejectedAccessToken);
        return await SendWithAccessTokenAsync(requestFactory, cancellationToken);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = session.GetSnapshot().RefreshToken;
        session.Clear();
        if (refreshToken is null) return;
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/logout",
            new LogoutRequest(refreshToken),
            JsonOptions,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowApiErrorAsync(response, cancellationToken);
    }

    private async Task RefreshAsync(
        CancellationToken cancellationToken,
        bool force,
        string? rejectedAccessToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        var revision = session.GetSnapshot().Revision;
        try
        {
            var snapshot = session.GetSnapshot();
            revision = snapshot.Revision;
            if (force && rejectedAccessToken is not null && snapshot.AccessToken != rejectedAccessToken) return;
            if (!force && snapshot.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddSeconds(30)) return;
            if (snapshot.RefreshToken is not { } refreshToken ||
                snapshot.RefreshTokenExpiresAtUtc <= DateTime.UtcNow)
            {
                session.ClearIfCurrent(revision);
                throw new ApiClientException(HttpStatusCode.Unauthorized, "The session has expired. Sign in again.");
            }

            using var response = await httpClient.PostAsJsonAsync(
                "api/auth/refresh",
                new RefreshTokenRequest(refreshToken),
                JsonOptions,
                cancellationToken);
            var tokens = await ReadTokenResponseAsync(response, cancellationToken);
            if (!session.TrySet(tokens, revision))
                throw new ApiClientException(HttpStatusCode.Unauthorized, "The session changed while tokens were refreshing.");
        }
        catch
        {
            session.ClearIfCurrent(revision);
            throw;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<HttpResponseMessage> SendWithAccessTokenAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        try
        {
            return await httpClient.SendAsync(request, cancellationToken);
        }
        finally
        {
            request.Dispose();
        }
    }

    private static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string uri, T body) => new(method, uri)
    {
        Content = JsonContent.Create(body, options: JsonOptions)
    };

    private static async Task<TokenResponse> ReadTokenResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            await ThrowApiErrorAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TokenResponse>(JsonOptions, cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "The API returned an empty authentication response.");
    }

    private static async Task ThrowApiErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ApiProblem? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
        }

        throw new ApiClientException(
            response.StatusCode,
            problem?.Detail ?? problem?.Title ?? $"The API returned {(int)response.StatusCode} {response.ReasonPhrase}.");
    }
}
