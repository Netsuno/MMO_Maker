# Modèle de données PostgreSQL (jalon carte)

Source de vérité : **PostgreSQL** (ADR-0002). Migrations EF Core dans `Frog.Persistence.PostgreSql/Migrations`.  
MariaDB reste un héritage temporaire (serveur/éditeur existants), plus de nouvelles tables.

Dates : `timestamptz` UTC.

## Schémas

| Schéma | Contenu |
| --- | --- |
| `world` | cartes, cellules, warps, spawns NPC |
| `content` | tilesets (définitions) |
| `ops` | imports legacy, historique EF |

## Tables

### `world.maps`

- `id` uuid PK — **identité canonique (`MapId`)**
- `name` varchar(200)
- `width`, `height` int CHECK > 0
- `allow_player_overlap` bool
- `status` smallint (`Draft=0`, `Published=1`)
- `revision` bigint CHECK >= 0 (concurrence optimiste)
- `layers_catalog_json` jsonb (ordre / nom / visible / locked des couches)
- `created_at_utc`, `updated_at_utc`

> Migration `ModernMapIdentity` : suppression de `legacy_id` (identité historique FRoG).

### `world.map_cells`

- PK `(map_id, x, y)`
- `layers_json` jsonb : pile ordonnée `{layerType, tileType, tilesetId, srcX, srcY, warpTargetMapId (Guid), warp*, scriptId}`
- CHECK `x >= 0 AND y >= 0`
- FK cascade vers `maps`

### `world.map_warps`

- PK uuid ; unique `(map_id, source_x, source_y)`
- `target_map_id` uuid NULL → FK `world.maps(id)` ON DELETE SET NULL
- `target_x`, `target_y`
- `destination_unresolved` si cible absente

### `world.map_npc_spawns`

- Placeholder (id, map_id, npc_definition_id, x, y, direction)

### `content.tilesets`

- `id` uuid PK
- `name` varchar(120)
- `logical_path` unique, `tile_size_pixels`, `width`/`height`, `sha256_hex`
- `editor_palette_id` int NULL unique (alias peinture cartes)
- `status` smallint (Draft/Published), `revision`, `published_revision`, `published_snapshot_id`
- `created_at_utc`, `updated_at_utc`

### `content.tileset_published_snapshots` / `tileset_publication_history`

- Snapshots immuables + historique de publication (même modèle que les cartes)

### `ops.legacy_imports`

- unique `(sha256_hex, format_type)` → import idempotent
- `report_json` jsonb, `result`, `source_path`, `imported_at_utc`

## Contrat applicatif (`Frog.Application`)

- `SaveMapRequest.MapId` — null ou Empty = création ; sinon mise à jour
- `LoadByIdAsync(Guid mapId)`
- `ITilesetRepository` / `TilesetWorkspaceSession` — draft/publish tilesets
- `IPublishedTilesetCatalog` — consommation serveur publiée uniquement

## Invariants applicatifs

- Sauvegarde dans une transaction (header + cellules + warps).
- `ExpectedRevision` doit matcher `revision` (0 = création).
- Le domaine `Map.Validate()` / `TilesetDefinition.Validate()` s’exécute avant écriture.
- Tuile warp : `WarpTargetMapId` est un **Guid** (Empty = invalide à la validation).

## Migrations

| Migration | Contenu |
| --- | --- |
| `InitialMapPersistence` | schémas + tables initiales |
| `ModernMapIdentity` | retrait `legacy_id` / `target_legacy_id` ; FK `target_map_id` |
| `DraftPublishSeparation` | snapshots cartes publiées |
| `TilesetDraftPublish` | draft/publish tilesets + snapshots |
