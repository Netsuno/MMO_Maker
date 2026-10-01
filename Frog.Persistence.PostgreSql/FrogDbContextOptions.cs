using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.PostgreSql;

public static class FrogDbContextOptions
{
    public static DbContextOptions<FrogDbContext> Create(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Multiplexing has caused intermittent ParseComplete/ReadyForQuery teardown failures
            // under concurrent Phase 7 server hosts in integration tests.
            Multiplexing = false,
        };
        return new DbContextOptionsBuilder<FrogDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    /// <summary>
    /// Borne le délai de connexion éditeur (secondes Npgsql <c>Timeout</c>).
    /// <see cref="Create"/> n’est pas modifié : le serveur et les tests d’intégration gardent leur délai.
    /// </summary>
    public static string WithEditorConnectTimeout(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Timeout <= 0 || builder.Timeout > 5)
        {
            builder.Timeout = 5;
        }

        return builder.ConnectionString;
    }
}
