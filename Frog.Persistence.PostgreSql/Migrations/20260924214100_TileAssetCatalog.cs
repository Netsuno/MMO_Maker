using Frog.Persistence.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frog.Persistence.PostgreSql.Migrations
{
    /// <summary>
    /// Additive TileAsset catalog: content.tiles, tile_packs, tile_pack_entries,
    /// tileset_imports, and the player.player_tile_unlocks stub.
    /// Legacy content.tilesets is not modified. Published/yanked packs are immutable via triggers.
    /// </summary>
    [DbContext(typeof(FrogDbContext))]
    [Migration("20260924214100_TileAssetCatalog")]
    public partial class TileAssetCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tiles",
                schema: "content",
                comment: "Individual 48×48 tile assets. PK TileAssetId = SHA-256 hex of normalized PNG.",
                columns: table => new
                {
                    tile_asset_id = table.Column<string>(
                        type: "char(64)",
                        fixedLength: true,
                        maxLength: 64,
                        nullable: false,
                        comment: "Content-addressed id; immutable. Map cells (format v6) reference this value."),
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    png_bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    width_px = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)48),
                    height_px = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)48),
                    display_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false, defaultValueSql: "'{}'"),
                    meta_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    content_bytes_len = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tiles", x => x.tile_asset_id);
                    table.UniqueConstraint("uq_tiles_id", x => x.id);
                    table.CheckConstraint("ck_tiles_tile_asset_id_sha256", "tile_asset_id ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_tiles_size_48", "width_px = 48 AND height_px = 48");
                    table.CheckConstraint(
                        "ck_tiles_png_nonempty",
                        "content_bytes_len > 0 AND octet_length(png_bytes) = content_bytes_len");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tiles_created_at_utc",
                schema: "content",
                table: "tiles",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                    name: "ix_tiles_tags_gin",
                    schema: "content",
                    table: "tiles",
                    column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                    name: "ix_tiles_meta_json_gin",
                    schema: "content",
                    table: "tiles",
                    column: "meta_json")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateTable(
                name: "tile_packs",
                schema: "content",
                comment: "Versioned tile packs. Published rows are immutable (enforce in app + trigger).",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    frogpack_sha256 = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: true),
                    frogpack_bytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    frogpack_bytes_len = table.Column<int>(type: "integer", nullable: true),
                    ed25519_signature = table.Column<byte[]>(type: "bytea", nullable: true),
                    ed25519_public_key_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    manifest_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tile_packs", x => x.id);
                    table.UniqueConstraint("uq_tile_packs_slug_version", x => new { x.slug, x.version });
                    table.CheckConstraint("ck_tile_packs_status", "status IN (0, 1, 2)");
                    table.CheckConstraint(
                        "ck_tile_packs_frogpack_sha",
                        "frogpack_sha256 IS NULL OR frogpack_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_tile_packs_entry_count", "entry_count >= 0");
                    table.CheckConstraint(
                        "ck_tile_packs_published_shape",
                        """
                        status <> 1 OR (frogpack_sha256 IS NOT NULL AND ed25519_signature IS NOT NULL AND published_at_utc IS NOT NULL AND entry_count > 0)
                        """);
                    table.CheckConstraint(
                        "ck_tile_packs_bytes_len",
                        """
                        frogpack_bytes IS NULL OR (frogpack_bytes_len IS NOT NULL AND frogpack_bytes_len > 0 AND octet_length(frogpack_bytes) = frogpack_bytes_len)
                        """);
                });

            migrationBuilder.CreateIndex(
                name: "uq_tile_packs_published_frogpack_sha",
                schema: "content",
                table: "tile_packs",
                column: "frogpack_sha256",
                unique: true,
                filter: "frogpack_sha256 IS NOT NULL AND status = 1");

            migrationBuilder.CreateIndex(
                name: "ix_tile_packs_slug_status",
                schema: "content",
                table: "tile_packs",
                columns: new[] { "slug", "status" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION content.trg_tile_packs_immutable_published()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                BEGIN
                    IF OLD.status IN (1, 2) THEN
                        IF NEW.slug IS DISTINCT FROM OLD.slug
                            OR NEW.version IS DISTINCT FROM OLD.version
                            OR NEW.frogpack_sha256 IS DISTINCT FROM OLD.frogpack_sha256
                            OR NEW.frogpack_bytes IS DISTINCT FROM OLD.frogpack_bytes
                            OR NEW.frogpack_bytes_len IS DISTINCT FROM OLD.frogpack_bytes_len
                            OR NEW.ed25519_signature IS DISTINCT FROM OLD.ed25519_signature
                            OR NEW.ed25519_public_key_id IS DISTINCT FROM OLD.ed25519_public_key_id
                            OR NEW.entry_count IS DISTINCT FROM OLD.entry_count
                            OR NEW.manifest_json IS DISTINCT FROM OLD.manifest_json
                            OR NEW.published_at_utc IS DISTINCT FROM OLD.published_at_utc
                        THEN
                            RAISE EXCEPTION 'content.tile_packs row % is immutable after publish/yank', OLD.id;
                        END IF;
                        -- status may only move published(1) → yanked(2)
                        IF NEW.status IS DISTINCT FROM OLD.status
                            AND NOT (OLD.status = 1 AND NEW.status = 2)
                        THEN
                            RAISE EXCEPTION 'content.tile_packs status transition % → % forbidden', OLD.status, NEW.status;
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$;

                DROP TRIGGER IF EXISTS tile_packs_immutable_published ON content.tile_packs;
                CREATE TRIGGER tile_packs_immutable_published
                    BEFORE UPDATE ON content.tile_packs
                    FOR EACH ROW
                    EXECUTE FUNCTION content.trg_tile_packs_immutable_published();
                """);

            migrationBuilder.CreateTable(
                name: "tile_pack_entries",
                schema: "content",
                comment: "Pack membership. FK RESTRICT on tiles so assets are not deleted while referenced.",
                columns: table => new
                {
                    pack_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_asset_id = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    entry_meta_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tile_pack_entries", x => new { x.pack_id, x.tile_asset_id });
                    table.CheckConstraint("ck_tile_pack_entries_ordinal", "ordinal >= 0");
                    table.ForeignKey(
                        name: "fk_tile_pack_entries_pack",
                        column: x => x.pack_id,
                        principalSchema: "content",
                        principalTable: "tile_packs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_tile_pack_entries_tile",
                        column: x => x.tile_asset_id,
                        principalSchema: "content",
                        principalTable: "tiles",
                        principalColumn: "tile_asset_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tile_pack_entries_tile_asset_id",
                schema: "content",
                table: "tile_pack_entries",
                column: "tile_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_tile_pack_entries_pack_ordinal",
                schema: "content",
                table: "tile_pack_entries",
                columns: new[] { "pack_id", "ordinal" });

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION content.trg_tile_pack_entries_pack_immutable()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    pack_status smallint;
                BEGIN
                    SELECT status INTO pack_status
                    FROM content.tile_packs
                    WHERE id = COALESCE(NEW.pack_id, OLD.pack_id);

                    IF pack_status IN (1, 2) THEN
                        RAISE EXCEPTION 'cannot mutate tile_pack_entries for immutable pack %', COALESCE(NEW.pack_id, OLD.pack_id);
                    END IF;
                    RETURN COALESCE(NEW, OLD);
                END;
                $$;

                DROP TRIGGER IF EXISTS tile_pack_entries_pack_immutable ON content.tile_pack_entries;
                CREATE TRIGGER tile_pack_entries_pack_immutable
                    BEFORE INSERT OR UPDATE OR DELETE ON content.tile_pack_entries
                    FOR EACH ROW
                    EXECUTE FUNCTION content.trg_tile_pack_entries_pack_immutable();
                """);

            migrationBuilder.CreateTable(
                name: "tileset_imports",
                schema: "content",
                comment: "Import audit trail (sheet → individual tiles). Runtime truth remains tiles + tile_packs.",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    source_tileset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_logical_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_sha256 = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: true),
                    source_png_bytes = table.Column<byte[]>(type: "bytea", nullable: true),
                    tile_size_px = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)48),
                    grid_width = table.Column<int>(type: "integer", nullable: false),
                    grid_height = table.Column<int>(type: "integer", nullable: false),
                    produced_pack_id = table.Column<Guid>(type: "uuid", nullable: true),
                    imported_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    importer = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    raw_meta_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    tile_asset_ids = table.Column<string[]>(type: "char(64)[]", nullable: false, defaultValueSql: "'{}'")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tileset_imports", x => x.id);
                    table.CheckConstraint("ck_tileset_imports_tile_size", "tile_size_px = 48");
                    table.CheckConstraint("ck_tileset_imports_grid", "grid_width > 0 AND grid_height > 0");
                    table.CheckConstraint(
                        "ck_tileset_imports_source_sha",
                        "source_sha256 IS NULL OR source_sha256 ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "fk_tileset_imports_tileset",
                        column: x => x.source_tileset_id,
                        principalSchema: "content",
                        principalTable: "tilesets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tileset_imports_pack",
                        column: x => x.produced_pack_id,
                        principalSchema: "content",
                        principalTable: "tile_packs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tileset_imports_source_tileset_id",
                schema: "content",
                table: "tileset_imports",
                column: "source_tileset_id");

            migrationBuilder.CreateIndex(
                name: "ix_tileset_imports_produced_pack_id",
                schema: "content",
                table: "tileset_imports",
                column: "produced_pack_id");

            migrationBuilder.CreateIndex(
                name: "ix_tileset_imports_imported_at_utc",
                schema: "content",
                table: "tileset_imports",
                column: "imported_at_utc",
                descending: new[] { true });

            migrationBuilder.CreateTable(
                name: "player_tile_unlocks",
                schema: "player",
                comment: "Stub for future per-character tile unlocks. Editor MUST NOT filter on this in V1.",
                columns: table => new
                {
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tile_asset_id = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false),
                    unlocked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_player_tile_unlocks", x => new { x.character_id, x.tile_asset_id });
                    table.ForeignKey(
                        name: "fk_player_tile_unlocks_character",
                        column: x => x.character_id,
                        principalSchema: "player",
                        principalTable: "characters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_player_tile_unlocks_tile",
                        column: x => x.tile_asset_id,
                        principalSchema: "content",
                        principalTable: "tiles",
                        principalColumn: "tile_asset_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_tile_unlocks_tile_asset_id",
                schema: "player",
                table: "player_tile_unlocks",
                column: "tile_asset_id");

            migrationBuilder.Sql(
                """
                CREATE OR REPLACE FUNCTION content.tile_pack_refresh_entry_count(p_pack_id uuid)
                RETURNS integer
                LANGUAGE sql
                AS $$
                    UPDATE content.tile_packs p
                    SET entry_count = sub.cnt
                    FROM (
                        SELECT count(*)::integer AS cnt
                        FROM content.tile_pack_entries e
                        WHERE e.pack_id = p_pack_id
                    ) sub
                    WHERE p.id = p_pack_id
                      AND p.status = 0
                    RETURNING p.entry_count;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "player_tile_unlocks",
                schema: "player");

            migrationBuilder.DropTable(
                name: "tileset_imports",
                schema: "content");

            migrationBuilder.Sql(
                """
                DROP FUNCTION IF EXISTS content.tile_pack_refresh_entry_count(uuid);
                """);

            migrationBuilder.DropTable(
                name: "tile_pack_entries",
                schema: "content");

            migrationBuilder.DropTable(
                name: "tile_packs",
                schema: "content");

            migrationBuilder.DropTable(
                name: "tiles",
                schema: "content");

            migrationBuilder.Sql(
                """
                DROP FUNCTION IF EXISTS content.trg_tile_pack_entries_pack_immutable();
                DROP FUNCTION IF EXISTS content.trg_tile_packs_immutable_published();
                """);
        }
    }
}
