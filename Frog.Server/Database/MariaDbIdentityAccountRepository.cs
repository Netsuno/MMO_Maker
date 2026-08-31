using System.Security.Cryptography;
using System.Text;
using Frog.Application.Identity;
using Frog.Core.Security;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Comptes MariaDB legacy — lecture hash+sel ; écriture PBKDF2 v1 dans password_hash. Non enregistré en production Phase 7 (auth = PostgreSQL ou in-memory test).</summary>
public sealed class MariaDbIdentityAccountRepository : IAccountRepository
{
    private readonly string _connectionString;

    public MariaDbIdentityAccountRepository(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public async Task<AccountRecord?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (!AccountInputRules.IsValidUsername(username))
        {
            return null;
        }

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            SELECT username, password_hash, password_salt, created_utc
            FROM accounts
            WHERE username = @username;
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var name = reader.GetString(0);
        var hash = reader.GetString(1);
        var salt = reader.GetString(2);
        var created = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);
        var stored = $"{hash}|{salt}";
        var id = CreateDeterministicAccountId(name);
        return new AccountRecord(id, name, stored, new DateTimeOffset(created));
    }

    public async Task<AccountRecord?> FindByIdAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new MySqlCommand(
            "SELECT username, password_hash, password_salt, created_utc FROM accounts;",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var name = reader.GetString(0);
            if (CreateDeterministicAccountId(name) != accountId)
            {
                continue;
            }

            var hash = reader.GetString(1);
            var salt = reader.GetString(2);
            var created = DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc);
            return new AccountRecord(accountId, name, $"{hash}|{salt}", new DateTimeOffset(created));
        }

        return null;
    }

    public async Task<AccountCreateResult> TryCreateAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (!AccountInputRules.IsValidUsername(username) || !AccountInputRules.IsValidPassword(password))
        {
            return new AccountCreateResult(AccountCreateStatus.InvalidInput);
        }

        var normalized = username.Trim();
        var passwordHash = PasswordHasher.HashPassword(password);

        await using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        const string sql = """
            INSERT INTO accounts(username, password_hash, password_salt, created_utc)
            VALUES (@username, @password_hash, @password_salt, @created_utc);
            """;

        await using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", normalized);
        command.Parameters.AddWithValue("@password_hash", passwordHash);
        command.Parameters.AddWithValue("@password_salt", string.Empty);
        command.Parameters.AddWithValue("@created_utc", DateTime.UtcNow);

        try
        {
            var rows = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return rows == 1
                ? new AccountCreateResult(AccountCreateStatus.Created, CreateDeterministicAccountId(normalized))
                : new AccountCreateResult(AccountCreateStatus.InvalidInput);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            return new AccountCreateResult(AccountCreateStatus.DuplicateUsername);
        }
    }

    private static Guid CreateDeterministicAccountId(string username)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("frog-account:" + username.Trim().ToUpperInvariant()));
        return new Guid(hash.AsSpan(0, 16));
    }
}
