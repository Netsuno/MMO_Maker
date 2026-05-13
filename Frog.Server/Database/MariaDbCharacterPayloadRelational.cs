using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Constantes et graines pour la persistance relationnelle du « payload » perso (stats, worldFlags).</summary>
internal static class MariaDbCharacterPayloadRelational
{
    public static readonly string[] StatCodes = ["STR", "AGI", "DEX", "INT", "VIT", "LUCK"];

    public static void SeedDefaultStats(MySqlConnection connection, string characterId, MySqlTransaction? transaction = null)
    {
        const string sql = """
            INSERT INTO character_stat(character_uuid, stat_code, value)
            VALUES (@id, @code, 10);
            """;

        foreach (var code in StatCodes)
        {
            using var cmd = new MySqlCommand(sql, connection, transaction);
            cmd.Parameters.AddWithValue("@id", characterId);
            cmd.Parameters.AddWithValue("@code", code);
            cmd.ExecuteNonQuery();
        }
    }
}
