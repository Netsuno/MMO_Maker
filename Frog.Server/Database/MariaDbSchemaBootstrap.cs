using System.Text;
using Frog.Core.Utils;
using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Applique le schéma SQL v1 (MariaDB / InnoDB) et assure un compte de démo si absent.
/// </summary>
public static class MariaDbSchemaBootstrap
{
    public static void Apply(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var sqlPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema_frog_mariadb_v1.sql");
        if (!File.Exists(sqlPath))
        {
            throw new FileNotFoundException("Schéma MariaDB introuvable (copie de sortie).", sqlPath);
        }

        var batch = File.ReadAllText(sqlPath, Encoding.UTF8);

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        foreach (var statement in SplitSqlStatements(batch))
        {
            using var cmd = new MySqlCommand(statement, connection);
            cmd.ExecuteNonQuery();
        }

        EnsurePlayerCharacterForeignKey(connection);
        MariaDbMigrationV2.Apply(connection);
        MariaDbMigrationV3.Apply(connection);
        MariaDbMigrationV4.Apply(connection);
        MariaDbMigrationV5.Apply(connection);
        MariaDbMigrationV6.Apply(connection);
        SeedDemoAccountIfMissing(connection);
    }

    private static void EnsurePlayerCharacterForeignKey(MySqlConnection connection)
    {
        const string checkSql = """
            SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND TABLE_NAME = 'player_world_state'
              AND CONSTRAINT_NAME = 'fk_pws_character';
            """;

        using (var check = new MySqlCommand(checkSql, connection))
        {
            var n = Convert.ToInt32(check.ExecuteScalar());
            if (n > 0)
            {
                return;
            }
        }

        const string alterSql = """
            ALTER TABLE player_world_state
            ADD CONSTRAINT fk_pws_character
            FOREIGN KEY (character_uuid) REFERENCES frog_character(id) ON DELETE SET NULL;
            """;

        using var alter = new MySqlCommand(alterSql, connection);
        alter.ExecuteNonQuery();
    }

    private static void SeedDemoAccountIfMissing(MySqlConnection connection)
    {
        const string existsSql = "SELECT 1 FROM accounts WHERE username = 'demo' LIMIT 1;";
        using var existsCommand = new MySqlCommand(existsSql, connection);
        if (existsCommand.ExecuteScalar() is not null)
        {
            return;
        }

        var (hash, salt) = HashHelper.HashPassword("demo");

        const string insertSql = """
            INSERT INTO accounts(username, password_hash, password_salt, created_utc)
            VALUES ('demo', @password_hash, @password_salt, @created_utc);
            """;

        using var insert = new MySqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("@password_hash", hash);
        insert.Parameters.AddWithValue("@password_salt", salt);
        insert.Parameters.AddWithValue("@created_utc", DateTime.UtcNow);
        insert.ExecuteNonQuery();
    }

    /// <summary>Découpe un script en instructions ; MySqlConnector n'expose pas MySqlScript.</summary>
    private static IEnumerable<string> SplitSqlStatements(string batch)
    {
        foreach (var part in batch.Split(new[] { ";\r\n", ";\n" }, StringSplitOptions.None))
        {
            var s = part.Trim();
            if (s.Length == 0)
            {
                continue;
            }

            // Retirer blocs de commentaires ligne par ligne en tête (reste du fichier après dernier ';')
            var lines = s.Split('\n', StringSplitOptions.None);
            var kept = new List<string>();
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                kept.Add(line);
            }

            s = string.Join('\n', kept).Trim();
            if (s.Length > 0)
            {
                yield return s;
            }
        }
    }
}
