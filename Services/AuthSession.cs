namespace AvaloniaApp.Services;

public sealed record AuthenticatedUser(
    Guid Id,
    string Username,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);

internal sealed record AuthSessionSnapshot(
    long Revision,
    string? AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string? RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    AuthenticatedUser? User);

public sealed class AuthSession
{
    private readonly object _sync = new();
    private long _revision;
    private string? _accessToken;
    private DateTime _accessTokenExpiresAtUtc;
    private string? _refreshToken;
    private DateTime _refreshTokenExpiresAtUtc;
    private AuthenticatedUser? _user;

    public event EventHandler? Changed;

    public string? AccessToken { get { lock (_sync) return _accessToken; } }
    public DateTime AccessTokenExpiresAtUtc { get { lock (_sync) return _accessTokenExpiresAtUtc; } }
    public string? RefreshToken { get { lock (_sync) return _refreshToken; } }
    public DateTime RefreshTokenExpiresAtUtc { get { lock (_sync) return _refreshTokenExpiresAtUtc; } }
    public AuthenticatedUser? User { get { lock (_sync) return _user; } }
    public bool IsAuthenticated { get { lock (_sync) return _accessToken is not null && _user is not null; } }

    public bool HasRole(string role)
    {
        lock (_sync)
        {
            return _user?.Roles.Contains(role, StringComparer.OrdinalIgnoreCase) == true;
        }
    }

    internal AuthSessionSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new AuthSessionSnapshot(
                _revision,
                _accessToken,
                _accessTokenExpiresAtUtc,
                _refreshToken,
                _refreshTokenExpiresAtUtc,
                _user);
        }
    }

    internal long BeginAuthentication()
    {
        long revision;
        lock (_sync)
        {
            revision = ++_revision;
            ClearValues();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return revision;
    }

    internal bool TrySet(TokenResponse response, long expectedRevision)
    {
        lock (_sync)
        {
            if (_revision != expectedRevision) return false;
            _accessToken = response.AccessToken;
            _accessTokenExpiresAtUtc = response.ExpiresAtUtc;
            _refreshToken = response.RefreshToken;
            _refreshTokenExpiresAtUtc = response.RefreshTokenExpiresAtUtc;
            _user = response.User;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    internal bool ClearIfCurrent(long expectedRevision)
    {
        lock (_sync)
        {
            if (_revision != expectedRevision) return false;
            _revision++;
            ClearValues();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Clear()
    {
        lock (_sync)
        {
            _revision++;
            ClearValues();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ClearValues()
    {
        _accessToken = null;
        _accessTokenExpiresAtUtc = default;
        _refreshToken = null;
        _refreshTokenExpiresAtUtc = default;
        _user = null;
    }
}
