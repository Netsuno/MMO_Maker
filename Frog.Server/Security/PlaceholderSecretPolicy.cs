namespace Frog.Server.Security;

/// <summary>
/// Committed config and Compose files ship placeholder credentials only.
/// A public bind plus a known placeholder is refused at host composition.
/// </summary>
public static class PlaceholderSecretPolicy
{
    public static readonly string[] KnownPlaceholders =
    [
        "NOT_A_PRODUCTION_SECRET",
        "changeme",
        "CHANGE_ME",
        "VOTRE_MOT_DE_PASSE",
        "frog_dev_only",
        "frog_test_local_only",
    ];

    public static bool ContainsKnownPlaceholder(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        foreach (var token in KnownPlaceholders)
        {
            if (connectionString.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MustRejectPublicBind(
        bool playtestEnabled,
        bool bindIsLoopback,
        bool postgreSqlEnabled,
        string? postgreSqlConnectionString,
        bool mariaDbEnabled,
        string? mariaDbConnectionString)
    {
        if (playtestEnabled || bindIsLoopback)
        {
            return false;
        }

        return (postgreSqlEnabled && ContainsKnownPlaceholder(postgreSqlConnectionString))
               || (mariaDbEnabled && ContainsKnownPlaceholder(mariaDbConnectionString));
    }
}
