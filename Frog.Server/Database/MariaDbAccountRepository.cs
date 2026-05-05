using Frog.Core.Utils;
using Frog.Server.Models;
using MySqlConnector;

namespace Frog.Server.Database;

public sealed class MariaDbAccountRepository : IAccountRepository
{
    private readonly string _connectionString;

    public MariaDbAccountRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public bool TryGetByUsername(string username, out Account account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT username, password_hash, password_salt, created_utc
            FROM accounts
            WHERE username = @username;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);

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
            CreatedUtc = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)
        };

        return true;
    }

    public bool Create(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        var (hash, salt) = HashHelper.HashPassword(password);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            INSERT INTO accounts(username, password_hash, password_salt, created_utc)
            VALUES (@username, @password_hash, @password_salt, @created_utc);
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@password_hash", hash);
        command.Parameters.AddWithValue("@password_salt", salt);
        command.Parameters.AddWithValue("@created_utc", DateTime.UtcNow);

        try
        {
            return command.ExecuteNonQuery() == 1;
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return false;
        }
    }
}
