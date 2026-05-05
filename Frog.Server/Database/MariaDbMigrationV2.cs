using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>
/// Migration schéma « v2 » idempotente : identifiant numérique stable <c>accounts.id</c> et
/// <c>frog_character.account_id</c> comme FK principale vers le compte (le login <c>username</c> reste PK des comptes pour l’instant).
/// </summary>
public static class MariaDbMigrationV2
{
    public static void Apply(MySqlConnection connection)
    {
        EnsureAccountsNumericId(connection);
        EnsureCharacterAccountIdForeignKey(connection);
    }

    private static void EnsureAccountsNumericId(MySqlConnection connection)
    {
        if (ColumnExists(connection, "accounts", "id"))
        {
            return;
        }

        const string sql = """
            ALTER TABLE accounts
            ADD COLUMN id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT UNIQUE;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureCharacterAccountIdForeignKey(MySqlConnection connection)
    {
        if (!ColumnExists(connection, "accounts", "id"))
        {
            return;
        }

        if (!ColumnExists(connection, "frog_character", "account_id"))
        {
            const string addCol = """
                ALTER TABLE frog_character
                ADD COLUMN account_id BIGINT UNSIGNED NULL;
                """;

            using (var cmd = new MySqlCommand(addCol, connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        const string backfill = """
            UPDATE frog_character fc
            INNER JOIN accounts a ON fc.account_username = a.username
            SET fc.account_id = a.id
            WHERE fc.account_id IS NULL OR fc.account_id <> a.id;
            """;

        using (var bf = new MySqlCommand(backfill, connection))
        {
            bf.ExecuteNonQuery();
        }

        var orphans = Count(connection, "SELECT COUNT(*) FROM frog_character WHERE account_id IS NULL;");
        if (orphans > 0)
        {
            throw new InvalidOperationException(
                $"Migration v2 : {orphans} ligne(s) frog_character sans account_id (account_username invalide?) — corrigez avant de relancer.");
        }

        if (!ForeignKeyExists(connection, "frog_character", "fk_frog_character_account_id"))
        {
            TryDropForeignKey(connection, "frog_character", "fk_fc_account");

            const string modify = """
                ALTER TABLE frog_character
                MODIFY COLUMN account_id BIGINT UNSIGNED NOT NULL;
                """;

            using (var m = new MySqlCommand(modify, connection))
            {
                m.ExecuteNonQuery();
            }

            const string addFk = """
                ALTER TABLE frog_character
                ADD CONSTRAINT fk_frog_character_account_id
                FOREIGN KEY (account_id) REFERENCES accounts(id) ON DELETE CASCADE;
                """;

            using (var fk = new MySqlCommand(addFk, connection))
            {
                fk.ExecuteNonQuery();
            }
        }
    }

    private static bool ColumnExists(MySqlConnection connection, string table, string column)
    {
        const string sql = """
            SELECT COUNT(*) FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @t
              AND COLUMN_NAME = @c;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@t", table);
        cmd.Parameters.AddWithValue("@c", column);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool ForeignKeyExists(MySqlConnection connection, string table, string fkName)
    {
        const string sql = """
            SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND TABLE_NAME = @t
              AND CONSTRAINT_NAME = @fk
              AND CONSTRAINT_TYPE = 'FOREIGN KEY';
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@t", table);
        cmd.Parameters.AddWithValue("@fk", fkName);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static void TryDropForeignKey(MySqlConnection connection, string table, string fkName)
    {
        if (!ForeignKeyExists(connection, table, fkName))
        {
            return;
        }

        using var cmd = new MySqlCommand($"ALTER TABLE `{table}` DROP FOREIGN KEY `{fkName}`;", connection);
        cmd.ExecuteNonQuery();
    }

    private static int Count(MySqlConnection connection, string sql)
    {
        using var cmd = new MySqlCommand(sql, connection);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
