using MySqlConnector;

namespace Frog.Server.Database;

public sealed class MariaDbCharacterBootstrap : ICharacterBootstrap
{
    private readonly string _connectionString;

    public MariaDbCharacterBootstrap(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public string EnsureDefaultHero(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string selectSql = """
            SELECT id FROM frog_character
            WHERE account_username = @username AND display_name = 'Hero'
            LIMIT 1;
            """;

        using (var select = new MySqlCommand(selectSql, connection))
        {
            select.Parameters.AddWithValue("@username", username);
            using var reader = select.ExecuteReader();
            if (reader.Read())
            {
                return reader.GetString(0);
            }
        }

        var id = Guid.NewGuid().ToString();
        const string insertSql = """
            INSERT INTO frog_character(id, account_username, display_name, payload)
            VALUES (@id, @username, 'Hero', CAST('{}' AS JSON));
            """;

        using var insert = new MySqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("@id", id);
        insert.Parameters.AddWithValue("@username", username);
        insert.ExecuteNonQuery();
        return id;
    }
}
