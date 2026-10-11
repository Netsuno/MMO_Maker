# Modèle de données PostgreSQL (jalon carte)

Source de vérité : **PostgreSQL** (ADR-0002). Migrations EF Core dans `Frog.Persistence.PostgreSql/Migrations`.
MariaDB reste un héritage temporaire (serveur/éditeur existants), plus de nouvelles tables.

Dates : `timestamptz` UTC.

## Schémas

| Schéma | Contenu |
| --- | --- |
| `world` | cartes, cellules, warps, spawns NPC |
| `content` | tilesets (définitions), objets de carte, tuiles composées |
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
- `mob_spawn_zones_json` jsonb NULL — zones de réapparition des monstres (migration `MapMobSpawnZonesJson`). Document camelCase : `formatVersion`, `zones[]` (`id`, `name`, `tileX`, `tileY`, `width`, `height`, `entries[]` avec `monsterId`, `label`, `quantity`, `respawnSeconds`). Hors `.fmap`, hors Hello. `NULL` = aucune zone
- `created_at_utc`, `updated_at_utc`

La même colonne `mob_spawn_zones_json` jsonb NULL est ajoutée sur `world.map_published_snapshots` (snapshot publié de la carte). Le serveur ne lit que ce snapshot pour faire réapparaître les monstres à l’intérieur des rectangles.

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

### `content.map_objects`

Objets de carte (props placables). Distinct des objets d’inventaire. Migration `MapObjectDraftPublish`. Hello inchangé (**11**).

- `id` uuid PK
- `name` varchar(120)
- `logical_path` varchar(500) unique
- `placement_id` varchar(64) unique — identité de pose dans la palette **Objets**
- `footprint_width_tiles`, `footprint_height_tiles` int CHECK > 0
- `width`, `height` int CHECK > 0 (pixels de l’image)
- `sha256hex` varchar(64)
- `status` smallint (Draft/Published), `revision` bigint CHECK >= 0
- `published_revision` bigint NULL, `published_snapshot_id` uuid NULL
- `created_at_utc`, `updated_at_utc`

### `content.map_object_published_snapshots` / `map_object_publication_history`

- Snapshot : `id` uuid PK, `map_object_id` FK cascade, `revision`, `published_at_utc`, copie du nom / chemin / `placement_id` / empreinte / taille, `sha256hex`, `png_bytes` bytea NULL
- Unique `(map_object_id, revision)`
- Historique : `id`, `map_object_id` FK cascade, `snapshot_id`, `revision`, `published_at_utc`

Un brouillon n’entre pas dans la palette de l’éditeur. Seul le snapshot publié y est repris.

### `content.composed_tilesets`

Tileset composé de tuiles **48×48** choisies. Les pixels restent dans `content.tiles`. `content.tilesets` (feuille) n’est pas modifié. Migration `ComposedTilesetDraftPublish`. Hello inchangé (**11**).

- `id` uuid PK
- `name` varchar(120)
- `logical_path` varchar(500) unique
- `members_json` jsonb NOT NULL, défaut `'[]'`, CHECK `jsonb_typeof = 'array'` — ordre des tuiles, objets `{ id, name }` (identifiant `TileAsset`, nom d’affichage optionnel). Pas les pixels
- `status` smallint (Draft/Published), `revision` bigint CHECK >= 0
- `published_revision` bigint NULL, `published_snapshot_id` uuid NULL
- `created_at_utc`, `updated_at_utc`

### `content.composed_tileset_published_snapshots` / `composed_tileset_publication_history`

- Snapshot : `id` uuid PK, `composed_tileset_id` FK cascade, `revision`, `published_at_utc`, `name`, `logical_path`, `members_json` jsonb CHECK tableau
- Unique `(composed_tileset_id, revision)`
- Historique : `id`, `composed_tileset_id` FK cascade, `snapshot_id`, `revision`, `published_at_utc`

La publication alimente la palette TileAsset de l’éditeur et la charge client **CTS1** (`GET /content/composed-tilesets`, signature Ed25519). Un catalogue JSON brut est refusé.

### `ops.legacy_imports`

- unique `(sha256_hex, format_type)` → import idempotent
- `report_json` jsonb, `result`, `source_path`, `imported_at_utc`

## Contrat applicatif (`Frog.Application`)

- `SaveMapRequest.MapId` — null ou Empty = création ; sinon mise à jour
- `LoadByIdAsync(Guid mapId)`
- `ITilesetRepository` / `TilesetWorkspaceSession` — draft/publish tilesets
- `IPublishedTilesetCatalog` — consommation serveur publiée uniquement
- `IMapObjectRepository` — brouillon / publication des objets de carte ; la palette ne consomme que le catalogue publié
- `IComposedTilesetRepository` — brouillon / publication des tuiles composées ; pixels dans `content.tiles`
- `IPublishedWorldCatalog.ListMobSpawnZonesAsync` — zones du snapshot publié, pas du brouillon

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
| `MapObjectDraftPublish` (`20261004120000`) | `content.map_objects` + snapshots / historique |
| `MapMobSpawnZonesJson` (`20261004233000`) | `mob_spawn_zones_json` sur `world.maps` et `world.map_published_snapshots` |
| `ComposedTilesetDraftPublish` (`20261005010000`) | `content.composed_tilesets` + snapshots / historique |
