namespace Frog.Server.Security;

/// <summary>
/// Fenêtres d'échec auth : 8 / 60 s par IP+username, 30 / 60 s par IP.
/// Login, register et reconnect partagent les mêmes seaux (pas de clé IP:port).
/// </summary>
public sealed class AuthRateLimiter
{
    public const int DefaultIpUserMaxFailures = 8;
    public const int DefaultIpMaxFailures = 30;

    private readonly LoginRateLimiter _ipUser;
    private readonly LoginRateLimiter _ip;

    public AuthRateLimiter(
        int ipUserMaxFailures = DefaultIpUserMaxFailures,
        int ipMaxFailures = DefaultIpMaxFailures,
        TimeSpan? window = null)
    {
        var span = window ?? TimeSpan.FromSeconds(60);
        _ipUser = new LoginRateLimiter(ipUserMaxFailures, span);
        _ip = new LoginRateLimiter(ipMaxFailures, span);
    }

    public bool TryAllow(string remoteEndPoint, string? username)
    {
        if (!_ip.TryAllow(AuthRateLimitKey.Ip(remoteEndPoint)))
        {
            return false;
        }

        var user = AuthRateLimitKey.NormalizeUsername(username);
        if (user.Length == 0)
        {
            return true;
        }

        return _ipUser.TryAllow(AuthRateLimitKey.IpUser(remoteEndPoint, user));
    }

    public void RegisterFailure(string remoteEndPoint, string? username)
    {
        _ip.RegisterFailure(AuthRateLimitKey.Ip(remoteEndPoint));
        var user = AuthRateLimitKey.NormalizeUsername(username);
        if (user.Length == 0)
        {
            return;
        }

        _ipUser.RegisterFailure(AuthRateLimitKey.IpUser(remoteEndPoint, user));
    }

    public void RegisterSuccess(string remoteEndPoint, string? username)
    {
        var user = AuthRateLimitKey.NormalizeUsername(username);
        if (user.Length == 0)
        {
            return;
        }

        // Succès : on libère le seau IP+user. Le seau IP reste (NAT / brute-force partagé).
        _ipUser.RegisterSuccess(AuthRateLimitKey.IpUser(remoteEndPoint, user));
    }
}
