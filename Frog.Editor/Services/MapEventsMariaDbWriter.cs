using Frog.Core.Protocol;
using MySqlConnector;

namespace Frog.Editor.Services;

/// <summary>Écritures MVP sur <c>frog_map_event</c> (aligné sur <c>MariaDbMigrationV4</c>).</summary>
public static class MapEventsMariaDbWriter
{
    /// <summary>Insère une entrée catalogue ; <paramref name="newId"/> = dernier auto-incrément si succès.</summary>
    public static bool TryInsertCatalog(string connectionString, string slug, string displayName, out int newId, out string errorMessage)
    {
        newId = 0;
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var s = MapEventCatalogNormalization.TryNormalizeSlug(slug);
        var d = MapEventCatalogNormalization.TryNormalizeDisplayName(displayName);
        if (s is null)
        {
            errorMessage = "Slug invalide (lettres minuscules, chiffres, _ ; ex. pnj_marchand).";
            return false;
        }

        if (d is null)
        {
            errorMessage = "Nom affiché invalide.";
            return false;
        }

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string insertSql = """
            INSERT INTO frog_event_catalog(slug, display_name)
            VALUES (@slug, @dn);
            """;
        using (var cmd = new MySqlCommand(insertSql, connection))
        {
            cmd.Parameters.AddWithValue("@slug", s);
            cmd.Parameters.AddWithValue("@dn", d);
            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                errorMessage = "Ce slug existe déjà dans le catalogue.";
                return false;
            }
            catch (MySqlException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        try
        {
            using var idCmd = new MySqlCommand("SELECT LAST_INSERT_ID();", connection);
            var scalar = idCmd.ExecuteScalar();
            if (scalar is null || scalar is DBNull)
            {
                errorMessage = "Insertion catalogue : LAST_INSERT_ID indisponible.";
                return false;
            }

            newId = Convert.ToInt32(scalar);
            return newId > 0;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>Supprime une entrée catalogue (les placements associés sont supprimés en cascade).</summary>
    public static bool TryDeleteCatalogById(string connectionString, int catalogId, out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (catalogId < 1)
        {
            errorMessage = "Identifiant catalogue invalide.";
            return false;
        }

        if (catalogId == 1)
        {
            errorMessage = "L'entrée catalogue id=1 (démo) est réservée au socle.";
            return false;
        }

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            DELETE FROM frog_event_catalog
            WHERE id = @id
            LIMIT 1;
            """;
        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", catalogId);
        try
        {
            var n = cmd.ExecuteNonQuery();
            if (n == 0)
            {
                errorMessage = "Aucune entrée catalogue supprimée (id inconnu).";
                return false;
            }

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary><c>true</c> si une ligne a été insérée ; <c>false</c> si doublon unique (ignored).</summary>
    public static bool TryInsertPlacement(
        string connectionString,
        int mapId,
        int eventCatalogId,
        int tileX,
        int tileY,
        string? triggerKind,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1 || eventCatalogId < 1)
        {
            errorMessage = "map_id et event_catalog_id doivent être ≥ 1.";
            return false;
        }

        var tk = MapEventTriggerNormalization.NormalizeTriggerKind(triggerKind);

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        const string sql = """
            INSERT IGNORE INTO frog_map_event(map_id, event_catalog_id, tile_x, tile_y, trigger_kind)
            VALUES (@mapId, @catalogId, @tx, @ty, @tk);
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        cmd.Parameters.AddWithValue("@catalogId", eventCatalogId);
        cmd.Parameters.AddWithValue("@tx", tileX);
        cmd.Parameters.AddWithValue("@ty", tileY);
        cmd.Parameters.AddWithValue("@tk", tk);
        try
        {
            var affected = cmd.ExecuteNonQuery();
            return affected > 0;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryUpdatePlacementTriggerKind(
        string connectionString,
        long rowId,
        int mapId,
        string? triggerKind,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1 || rowId < 1)
        {
            errorMessage = "Identifiants invalides.";
            return false;
        }

        var tk = MapEventTriggerNormalization.NormalizeTriggerKind(triggerKind);

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            UPDATE frog_map_event
            SET trigger_kind = @tk
            WHERE id = @id AND map_id = @mapId
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@tk", tk);
        cmd.Parameters.AddWithValue("@id", rowId);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        try
        {
            var n = cmd.ExecuteNonQuery();
            if (n == 0)
            {
                errorMessage = "Aucune ligne mise à jour (id ou carte incorrect).";
                return false;
            }

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryDeletePlacement(string connectionString, long rowId, int mapId, out string errorMessage)
    {
        errorMessage = string.Empty;
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (mapId < 1 || rowId < 1)
        {
            errorMessage = "Identifiants invalides.";
            return false;
        }

        using var connection = new MySqlConnection(connectionString);
        connection.Open();
        const string sql = """
            DELETE FROM frog_map_event
            WHERE id = @id AND map_id = @mapId
            LIMIT 1;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", rowId);
        cmd.Parameters.AddWithValue("@mapId", mapId);
        try
        {
            var n = cmd.ExecuteNonQuery();
            if (n == 0)
            {
                errorMessage = "Aucune ligne supprimée (id ou carte incorrect).";
                return false;
            }

            return true;
        }
        catch (MySqlException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
