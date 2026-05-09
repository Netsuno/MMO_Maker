using MySqlConnector;

namespace Frog.Server.Database;

/// <summary>Tables catalogue / placement d’événements carte (MVP Phase 3).</summary>
public static class MariaDbMigrationV4
{
    public static void Apply(MySqlConnection connection)
    {
        EnsureEventCatalog(connection);
        EnsureMapEvent(connection);
        SeedDemoCatalogRow(connection);
    }

    private static void EnsureEventCatalog(MySqlConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS frog_event_catalog(
                id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                slug VARCHAR(64) NOT NULL,
                display_name VARCHAR(255) NOT NULL,
                created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                UNIQUE KEY uq_frog_event_catalog_slug(slug)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureMapEvent(MySqlConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS frog_map_event(
                id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                map_id INT NOT NULL,
                event_catalog_id INT NOT NULL,
                tile_x INT NOT NULL,
                tile_y INT NOT NULL,
                trigger_kind VARCHAR(32) NOT NULL DEFAULT 'interact' COMMENT 'interact | step_on | page',
                created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                CONSTRAINT fk_fme_map FOREIGN KEY (map_id) REFERENCES frog_map(id) ON DELETE CASCADE,
                CONSTRAINT fk_fme_cat FOREIGN KEY (event_catalog_id) REFERENCES frog_event_catalog(id) ON DELETE CASCADE,
                UNIQUE KEY uq_frog_map_event_cell(map_id, tile_x, tile_y, event_catalog_id)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    private static void SeedDemoCatalogRow(MySqlConnection connection)
    {
        const string sql = """
            INSERT IGNORE INTO frog_event_catalog(id, slug, display_name)
            VALUES (1, 'demo_interact', 'Interaction démo');
            """;

        using var cmd = new MySqlCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }
}
