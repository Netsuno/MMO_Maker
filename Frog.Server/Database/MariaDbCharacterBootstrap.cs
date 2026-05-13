using System.Collections.Generic;
using Frog.Core;
using Frog.Core.Models;
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

        var withPayloadColumn = MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload");

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
        if (withPayloadColumn)
        {
            const string insertSql = """
                INSERT INTO frog_character(id, account_id, account_username, display_name, payload)
                SELECT @id, a.id, @username, 'Hero', CAST(@payload AS JSON)
                FROM accounts a
                WHERE a.username = @username
                LIMIT 1;
                """;

            using var tx = connection.BeginTransaction();
            try
            {
                using (var insert = new MySqlCommand(insertSql, connection, tx))
                {
                    insert.Parameters.AddWithValue("@id", id);
                    insert.Parameters.AddWithValue("@username", username);
                    insert.Parameters.AddWithValue("@payload", CharacterPayloadDefaults.EmptyPayloadJson);
                    var n = insert.ExecuteNonQuery();
                    if (n != 1)
                    {
                        throw new InvalidOperationException($"Compte introuvable pour créer le perso : {username}");
                    }
                }

                MariaDbCharacterPayloadRelational.SeedDefaultStats(connection, id, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            return id;
        }

        const string insertNoPayload = """
            INSERT INTO frog_character(id, account_id, account_username, display_name)
            SELECT @id, a.id, @username, 'Hero'
            FROM accounts a
            WHERE a.username = @username
            LIMIT 1;
            """;

        using var txNp = connection.BeginTransaction();
        try
        {
            using (var insert = new MySqlCommand(insertNoPayload, connection, txNp))
            {
                insert.Parameters.AddWithValue("@id", id);
                insert.Parameters.AddWithValue("@username", username);
                var n = insert.ExecuteNonQuery();
                if (n != 1)
                {
                    throw new InvalidOperationException($"Compte introuvable pour créer le perso : {username}");
                }
            }

            MariaDbCharacterPayloadRelational.SeedDefaultStats(connection, id, txNp);
            txNp.Commit();
        }
        catch
        {
            txNp.Rollback();
            throw;
        }

        return id;
    }

    public IReadOnlyList<CharacterSlotInfo> ListCharacters(string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT id, display_name
            FROM frog_character
            WHERE account_username = @username
            ORDER BY created_at ASC, id ASC;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@username", username);
        using var reader = cmd.ExecuteReader();
        var list = new List<CharacterSlotInfo>();
        while (reader.Read())
        {
            list.Add(new CharacterSlotInfo(reader.GetString(0), reader.GetString(1)));
        }

        return list;
    }

    public bool IsCharacterOwned(string username, string characterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        const string sql = """
            SELECT 1 FROM frog_character
            WHERE account_username = @username AND id = @id
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@id", characterId);
        return cmd.ExecuteScalar() is not null;
    }

    public bool TryCreateCharacter(string username, string displayName, out string characterId, out string errorMessage)
    {
        characterId = string.Empty;
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        if (!CharacterDisplayNameRules.TryNormalize(displayName, out var name, out errorMessage))
        {
            return false;
        }

        using var connection = new MySqlConnection(_connectionString);
        connection.Open();

        var withPayloadColumn = MariaDbSchemaInfo.ColumnExists(connection, "frog_character", "payload");

        const string countSql = """
            SELECT COUNT(*) FROM frog_character
            WHERE account_username = @username;
            """;

        using (var countCmd = new MySqlCommand(countSql, connection))
        {
            countCmd.Parameters.AddWithValue("@username", username);
            var n = Convert.ToInt32(countCmd.ExecuteScalar());
            if (n >= 8)
            {
                errorMessage = "Nombre max. de persos atteint (8).";
                return false;
            }
        }

        var id = Guid.NewGuid().ToString();
        try
        {
            using var tx = connection.BeginTransaction();
            if (withPayloadColumn)
            {
                const string insertSql = """
                    INSERT INTO frog_character(id, account_id, account_username, display_name, payload)
                    SELECT @id, a.id, @username, @display_name, CAST(@payload AS JSON)
                    FROM accounts a
                    WHERE a.username = @username
                    LIMIT 1;
                    """;

                using (var insert = new MySqlCommand(insertSql, connection, tx))
                {
                    insert.Parameters.AddWithValue("@id", id);
                    insert.Parameters.AddWithValue("@username", username);
                    insert.Parameters.AddWithValue("@display_name", name);
                    insert.Parameters.AddWithValue("@payload", CharacterPayloadDefaults.EmptyPayloadJson);
                    var rows = insert.ExecuteNonQuery();
                    if (rows != 1)
                    {
                        tx.Rollback();
                        errorMessage = "Compte introuvable.";
                        return false;
                    }
                }
            }
            else
            {
                const string insertNoPayload = """
                    INSERT INTO frog_character(id, account_id, account_username, display_name)
                    SELECT @id, a.id, @username, @display_name
                    FROM accounts a
                    WHERE a.username = @username
                    LIMIT 1;
                    """;

                using (var insert = new MySqlCommand(insertNoPayload, connection, tx))
                {
                    insert.Parameters.AddWithValue("@id", id);
                    insert.Parameters.AddWithValue("@username", username);
                    insert.Parameters.AddWithValue("@display_name", name);
                    var rows = insert.ExecuteNonQuery();
                    if (rows != 1)
                    {
                        tx.Rollback();
                        errorMessage = "Compte introuvable.";
                        return false;
                    }
                }
            }

            MariaDbCharacterPayloadRelational.SeedDefaultStats(connection, id, tx);
            tx.Commit();
            characterId = id;
            return true;
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            errorMessage = "Ce nom de perso est deja utilise.";
            return false;
        }
    }
}
