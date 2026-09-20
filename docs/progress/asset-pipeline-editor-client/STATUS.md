# STATUS — Asset pipeline editor → client (MVP)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Import projet + placement carte + sidecars / catalogue → rendu client |
| **Propriétaire** | Netsun |
| **Statut** | MVP branché sur les fils réels (`MainForm`, `TilesetCache`, `ClientTilesetLoader`, `MapViewRenderer`, `PublishedCatalogWire`) |
| **Base** | `main` @ `eb731de` |
| **Branche** | `cursor/asset-pipeline-editor-client` |
| **PR** | Draft [#14](https://github.com/Netsuno/MMO_Maker/pull/14) vers `main` — **pas de merge** |
| **Tip** | `e8ae65c` |
| **CI** | [35473026295](https://github.com/Netsuno/MMO_Maker/actions/runs/35473026295) **SUCCESS** (`build-and-test` + `postgres-integration`) |

Protocole fil **inchangé** (`FrogWireProtocol.Version = 11`). Champ JSON catalogue `tilesets` **additif** (même politique que `recipes`).

Aucun claim de publication publique. Pas de second système de cartes / assets folklore.

---

## Ce qui marche (MVP)

1. **Import éditeur** — `ProjectAssetImporter` copie png/jpg/bmp/gif/webp sous `{racine}/tiles|sprites|icons|other/`, SHA-256, IHDR PNG. UI : Données de jeu **Importer…** (tilesets, NPC, objets, sorts, ressources) + menu **Importer un asset projet…** / **Charger une image tuiles…**.
2. **Placement** — l’import tileset charge `TilesetCache` avec `EditorPaletteId` ; le pinceau `MapCanvas` existant pose `Tile.TilesetId`.
3. **Publish / sync fichiers** — export `.fmap` copie `{id}.png` + `{stem}.tilesets.json` ; playtest (`EditorPlaytestTilesetSidecar`) écrit le layout `Tilesets/` + `Maps/` dans le workspace **et** le répertoire de `Frog.Client`.
4. **Client reçoit + dessine** — `ClientTilesetLoader` inchangé (fichiers locaux). `PublishedCatalogResult.tilesets[].pngBase64` → `ClientPublishedTilesetMaterializer` → `Tilesets/{paletteId}.png` → `ReloadTilesetBitmaps` / `MapViewRenderer` (plus les couleurs de secours).

---

## Différé (pas dans ce MVP)

- Sprites NPC / objets / joueurs **sur la carte** (le client dessine encore des ellipses joueur ; icônes Game Data = aperçu éditeur seulement).
- Paquet fil dédié tileset (pas de bump v12) ; `MapData` reste le blob `.fmap` exact.
- Stockage PNG en base (`frog_asset_blob` documenté, pas de consommateur).
- Alignement automatique `TilesetDefinition.Id` (Guid) ↔ `Tile.TilesetId` (int) hors `EditorPaletteId`.
- Ouverture d’une carte catalogue PostgreSQL **sans** recharger les PNG (le cache session n’est pas reconstruit depuis le Guid publié).
- Animation / atlases multi-frames.

---

## Hors scope volontaire

- Pas de stub `MapEditorForm` / `MapRenderer` / `EntityRenderer`.
- Pas de nouveau `PacketId`.
- Tests existants non assouplis.
