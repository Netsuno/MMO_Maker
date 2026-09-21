# STATUS — Asset pipeline editor → client (tilesets on client maps)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Import projet + placement carte + sidecars / catalogue → rendu client |
| **Propriétaire** | Netsun |
| **Statut** | Tilesets éditeur visibles sur le client (publish PostgreSQL + playtest) |
| **Base** | `main` |
| **PR** | `fix(assets): editor tilesets appear on client maps` |

Protocole fil **inchangé** (`FrogWireProtocol.Version` inchangé). Champ JSON catalogue `tilesets` / `pngBase64` **additif**. Sidecar playtest `published-tilesets.json` (filesystem, à côté du manifeste).

---

## Ce qui marche maintenant

1. **Import éditeur** — `ProjectAssetImporter` + `TilesetCache` (`EditorPaletteId` / id pinceau). Game Data **Publier** attache le PNG au snapshot.
2. **Publication carte** — `MapPublishedTilesetSync` publie chaque `Tile.TilesetId` utilisé avec le PNG de session (`EditorPaletteId` = id carte). Snapshot PostgreSQL `content.tileset_published_snapshots.png_bytes`.
3. **Ouverture carte catalogue** — `PublishedTilesetCacheHydrator` reconstruit `TilesetCache` depuis le Guid / palette publié + PNG (plus besoin de recharger les fichiers à la main).
4. **Playtest** — sidecar `Tilesets/` (workspace + dir client) **et** `published-tilesets.json`. Le serveur playtest (sans PostgreSQL) charge ce JSON comme `IPublishedTilesetCatalog` et envoie `pngBase64`.
5. **Client** — `PublishedCatalogResult.tilesets[].pngBase64` → matérialisation `Tilesets/{paletteId}.png` (exe **et** cwd) → `ClientTilesetLoader` / `MapViewRenderer`. Couverture : un id carte sans PNG matérialisable échoue les tests.

Alignement Guid ↔ int : `TilesetPaletteAlignment` (palette d’abord, puis SHA, puis correspondance 1-1 sur la carte).

---

## Différé (toujours hors scope)

- Sprites NPC / objets / joueurs **sur la carte** (ellipses joueur ; icônes Game Data = aperçu éditeur).
- Paquet fil dédié tileset (pas de bump) ; `MapData` reste le blob `.fmap` exact.
- Table `frog_asset_blob` MariaDB (le PNG publié vit sur le snapshot tileset PostgreSQL).
- Animation / atlases multi-frames.

---

## Hors scope volontaire

- Pas de stub `MapEditorForm` / `MapRenderer` / `EntityRenderer`.
- Pas de nouveau `PacketId`.
- Tests existants non assouplis.
