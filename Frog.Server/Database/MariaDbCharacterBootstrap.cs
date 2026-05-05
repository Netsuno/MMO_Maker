using Frog.Core;
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
            INSERT INTO frog_character(id, account_id, account_username, display_name, payload)
            SELECT @id, a.id, @username, 'Hero', CAST(@payload AS JSON)
            FROM accounts a
            WHERE a.username = @username
            LIMIT 1;
            """;

        using var insert = new MySqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("@id", id);
        insert.Parameters.AddWithValue("@username", username);
        insert.Parameters.AddWithValue("@payload", CharacterPayloadDefaults.NewHeroJson);
        var n = insert.ExecuteNonQuery();
        if (n != 1)
        {
            throw new InvalidOperationException($"Compte introuvable pour créer le perso : {username}");
        }

        return id;
    }
}
