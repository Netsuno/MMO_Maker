using System.Text;
using Frog.Core.Utils;
using Npgsql;

namespace Frog.Server.Database;

/// <summary>
/// Applique le schéma SQL v1 et assure un compte de démo si absent (même comportement qu'avant).
/// </summary>
public static class PostgresSchemaBootstrap
{
    public static void Apply(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var sqlPath = Path.Combine(AppContext.BaseDirectory, "Database", "schema_frog_persistence_v1.sql");
        if (!File.Exists(sqlPath))
        {
            throw new FileNotFoundException("Schéma PostgreSQL introuvable (copie de sortie).", sqlPath);
        }

        var batch = File.ReadAllText(sqlPath, Encoding.UTF8);

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        using (var cmd = new NpgsqlCommand(batch, connection))
        {
            cmd.ExecuteNonQuery();
        }

        SeedDemoAccountIfMissing(connection);
    }

    private static void SeedDemoAccountIfMissing(NpgsqlConnection connection)
    {
        const string existsSql = "SELECT 1 FROM accounts WHERE username = 'demo' LIMIT 1;";
        using var existsCommand = new NpgsqlCommand(existsSql, connection);
        if (existsCommand.ExecuteScalar() is not null)
        {
            return;
        }

        var (hash, salt) = HashHelper.HashPassword("demo");

        const string insertSql = """
            INSERT INTO accounts(username, password_hash, password_salt, created_utc)
            VALUES ('demo', @password_hash, @password_salt, @created_utc);
            """;

        using var insert = new NpgsqlCommand(insertSql, connection);
        insert.Parameters.AddWithValue("password_hash", hash);
        insert.Parameters.AddWithValue("password_salt", salt);
        insert.Parameters.AddWithValue("created_utc", DateTime.UtcNow);
        insert.ExecuteNonQuery();
    }
}
