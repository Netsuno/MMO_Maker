using System.Net;

namespace Frog.Server.Security;

/// <summary>
/// Clé d'auth rate-limit : IP normalisée (sans port, IPv4-mapped déplié) + username.
/// Deux connexions avec des ports source distincts partagent la même fenêtre.
/// </summary>
public static class AuthRateLimitKey
{
    public const string Unknown = "unknown";

    public static string NormalizeIp(string? remoteEndPoint)
    {
        if (string.IsNullOrWhiteSpace(remoteEndPoint) || remoteEndPoint == "<unknown>")
        {
            return Unknown;
        }

        var raw = remoteEndPoint.Trim();
        if (IPEndPoint.TryParse(raw, out var endPoint))
        {
            return FormatAddress(endPoint.Address);
        }

        if (IPAddress.TryParse(raw, out var address))
        {
            return FormatAddress(address);
        }

        return raw;
    }

    public static string Ip(string? remoteEndPoint) => "ip:" + NormalizeIp(remoteEndPoint);

    public static string IpUser(string? remoteEndPoint, string? username)
    {
        var user = NormalizeUsername(username);
        if (user.Length == 0)
        {
            return Ip(remoteEndPoint);
        }

        return "ipuser:" + NormalizeIp(remoteEndPoint) + ":" + user;
    }

    public static string NormalizeUsername(string? username)
        => string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim().ToLowerInvariant();

    private static string FormatAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.ToString();
    }
}
