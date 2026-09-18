using Npgsql;

namespace Frog.Persistence.IntegrationTests.Support;

/// <summary>Disposable PostgreSQL database created from the admin connection string.</summary>
internal sealed class DisposablePostgresDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;
    private bool _dropped;

    private DisposablePostgresDatabase(string adminConnectionString, string databaseName, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        DatabaseName = databaseName;
        ConnectionString = connectionString;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public static async Task<DisposablePostgresDatabase> CreateAsync(
        string adminConnectionString,
        string namePrefix = "frog_p93_")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConnectionString);
        var databaseName = namePrefix + Guid.NewGuid().ToString("N")[..12];
        await using (var conn = new NpgsqlConnection(adminConnectionString))
        {
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand($"CREATE DATABASE {databaseName};", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = databaseName,
        };
        return new DisposablePostgresDatabase(adminConnectionString, databaseName, builder.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dropped)
        {
            return;
        }

        _dropped = true;
        NpgsqlConnection.ClearAllPools();
        try
        {
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
                terminate.Parameters.AddWithValue("name", DatabaseName);
                await terminate.ExecuteNonQueryAsync();
            }

            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName};", conn);
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Teardown best-effort; leftover DBs are disposable test names.
        }
    }
}
