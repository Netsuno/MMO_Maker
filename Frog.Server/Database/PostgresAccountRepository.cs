using Frog.Core.Utils;
using Frog.Server.Models;
using Npgsql;

namespace Frog.Server.Database;

public sealed class PostgresAccountRepository : IAccountRepository
{
    private readonly string _connectionString;

    public PostgresAccountRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
        EnsureSchemaAndSeed();
    }

    public bool TryGetByUsername(string username, out Account account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT username, password_hash, password_salt, created_utc
            FROM accounts
            WHERE username = @username;
            """;

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            account = null!;
            return false;
        }

        account = new Account
        {
            Username = reader.GetString(0),
            PasswordHash = reader.GetString(1),
            PasswordSalt = reader.GetString(2),
            CreatedUtc = reader.GetDateTime(3)
        };

        return true;
    }

    public bool Create(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        var (hash, salt) = HashHelper.HashPassword(password);

        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO accounts(username, password_hash, password_salt, created_utc)
            VALUES (@username, @password_hash, @password_salt, @created_utc)
            ON CONFLICT (username) DO NOTHING;
            """;

        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("username", username);
        command.Parameters.AddWithValue("password_hash", hash);
        command.Parameters.AddWithValue("password_salt", salt);
        command.Parameters.AddWithValue("created_utc", DateTime.UtcNow);

        return command.ExecuteNonQuery() == 1;
    }

    private void EnsureSchemaAndSeed()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS accounts(
                username TEXT PRIMARY KEY,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL,
                created_utc TIMESTAMPTZ NOT NULL
            );
            """;

        using (var createTable = new NpgsqlCommand(createTableSql, connection))
        {
            createTable.ExecuteNonQuery();
        }

        // Seed minimal pour garder le même bootstrap que Sprint 1.
        const string existsSql = "SELECT 1 FROM accounts WHERE username = 'demo' LIMIT 1;";
        using var existsCommand = new NpgsqlCommand(existsSql, connection);
        var exists = existsCommand.ExecuteScalar() is not null;
        if (!exists)
        {
            _ = Create("demo", "demo");
        }
    }
}
