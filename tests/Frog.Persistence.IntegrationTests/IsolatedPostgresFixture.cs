using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.IntegrationTests;

/// <summary>Crée une base PostgreSQL isolée, applique les migrations, la détruit à la fin.</summary>
public sealed class IsolatedPostgresFixture : IAsyncLifetime
{
    public const string ConnectionEnvironmentVariable = "FROG_POSTGRES_TEST_CONNECTION_STRING";

    private string? _adminConnectionString;
    private string? _databaseName;

    public string ConnectionString { get; private set; } = string.Empty;

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable));

    public async Task InitializeAsync()
    {
        _adminConnectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(_adminConnectionString))
        {
            return;
        }

        _databaseName = "frog_it_" + Guid.NewGuid().ToString("N")[..12];
        await using (var conn = new NpgsqlConnection(_adminConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {_databaseName};", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = _databaseName,
        };
        ConnectionString = builder.ConnectionString;

        await using var db = new Frog.Persistence.PostgreSql.FrogDbContext(
            Frog.Persistence.PostgreSql.FrogDbContextOptions.Create(ConnectionString));
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_adminConnectionString) || string.IsNullOrWhiteSpace(_databaseName))
        {
            return;
        }

        await using var conn = new NpgsqlConnection(_adminConnectionString);
        await conn.OpenAsync();
        await using (var terminate = new NpgsqlCommand(
            """
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @name AND pid <> pg_backend_pid();
            """,
            conn))
        {
            terminate.Parameters.AddWithValue("name", _databaseName);
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_databaseName};", conn);
        await drop.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("PostgresIsolated")]
public sealed class PostgresCollection : ICollectionFixture<IsolatedPostgresFixture>
{
}
