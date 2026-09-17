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
}
