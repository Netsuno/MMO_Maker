# Pipeline tuiles 48×48

← [Référence](README.md)

Propriétaire : **Netsun**. Tip : `2af2dd22`. Hello TCP : `FrogWireProtocol.Version` = **11**.

Parcours d’une tuile de contenu, de l’identifiant `TileAsset` jusqu’à la charge **CTS1** lue par le client. Les classes, tables et paquets ci-dessous sont ceux du code à ce tip.

## Deux tailles

| Constante | Valeur | Rôle |
| --- | --- | --- |
| `WorldMetrics.DefaultTileSizePixels` | **32** | Grille monde et combat : mêlée, échange, pas, collision, centre de tuile (`TileCenterToPixels`) |
| `TileAssetMetrics.TargetTileSizePixels` | **48** | Tuile de contenu : `TileAsset`, carte v6, frogpack, CTS1 |

`CanonicalPixelByteCount` = 48 × 48 × 4 = **9216** octets RGBA prémultipliés. L’identifiant `TileAssetId` est le SHA-256 de ce buffer.

**Lacune documentée.** Un tileset *feuille* neuf part de **32**, la même valeur que la grille monde, et pas de 48.

- `TilesetDefinition.TileSizePixels` est initialisé au littéral `32`.
- Le panneau Tilesets (`GameDataForm`, bouton Nouveau) écrit aussi le littéral `32` dans `TileSizePixels`, `WidthPixels` et `HeightPixels`.
- Ce littéral n’est pas lu depuis `WorldMetrics.DefaultTileSizePixels`, même si les deux valent 32.
- `TileImportOptions.SourceCellPixels` part de `WorldMetrics.DefaultTileSizePixels` (32) et ne sert que si `ExplicitResize` est vrai. Le champ « Cellule source » du plan de travail TileAsset démarre à 32 et reste inactif tant que « Redimensionner explicitement vers 48 px » n’est pas coché.

Les tuiles composées (#148) n’utilisent pas ce défaut : chaque membre doit faire 48×48.

## 1. TileAsset

| | |
| --- | --- |
| Classes | `TileAsset`, `TileAssetId`, `TileAssetMetrics`, `TilePixelNormalizer` |
| Entrée | `TileAsset.FromStraightRgba` (alpha droit → prémultiplié) ou `FromNormalizedRgba` |
| Taille | `WidthPixels` / `HeightPixels` = 48 |
| Stockage pixels | `content.tiles` (`ContentTileEntity`, largeur et hauteur 48 à la publication d’un tileset composé) |

Pas de paquet TCP. Le hash tient lieu d’identité. Une carte ne stocke pas la position dans une palette.

## 2. TilePack (frogpack)

| | |
| --- | --- |
| Classes | `FrogPackFormat` (magic **FPK1**), `FrogPackWriter`, `FrogPackReader`, `FrogPackKeys` |
| Taille annoncée | `TileAssetMetrics.TargetTileSizePixels` (48) dans l’en-tête. Une autre taille est refusée, sans mise à l’échelle |
| Signature | Ed25519 sur le SHA-256 du corps. Même clé que CTS1 |
| Tables | `content.tile_packs`, `content.tile_pack_entries` |
| HTTP | `GET /content/tile-packs/current` (manifeste), `GET /content/tile-packs/current.frogpack` (octets) |
| Serveur | `TilePackContentHttp.HandleAsync` |
| Client | `MainShellForm.SyncTilePackAsync` (méthode privée) appelle d’abord le sync frogpack |

Hello ne porte pas ce fichier. Le protocole reste **11**.

## 3. Carte v6

| | |
| --- | --- |
| Classes | `MapFormat` (`Version6` = 6, `CurrentWriteVersion`), `MapFormat.CreateTileAssetMap`, `MapFormat.Write`, `MapSerializer` |
| Champ | `Map.TileSizePixels` = **48** et `GraphicIdentity` = `TileAsset` |
| Refus | une carte TileAsset dont `TileSizePixels` n’est pas 48 (`Map.Validate`, lecture v6). Pas d’upscale silencieux depuis 32 |
| Feuille | une carte `SheetSource` reste en **v5**. `TileSizePixels` 0 est la feuille ; l’affichage retombe alors sur la grille 32 |

Le blob `.fmap` ne contient pas les zones de monstres ni les tilesets composés.

## 4. Tuiles composées

Choix ordonné de tuiles déjà en 48×48. Ce n’est pas un découpage de feuille. `content.tilesets` n’est pas modifié.

| Étape | Classe / table |
| --- | --- |
| Éditeur | `ComposedTilesetEditorPanel.InitializeAsync`, `ComposedTilesetWorkspaceSession` |
| Domaine | `ComposedTilesetDefinition.Validate` / `Normalize`, `ComposedTileRef` |
| JSON des membres | `ComposedTilesetMemberCodec.Write` / `Read` → `content.composed_tilesets.members_json` (`{ id, name }`, sans pixels) |
| Pixels | `PostgresComposedTilesetRepository` écrit le RGBA dans `content.tiles` |
| Brouillon / snapshot | `content.composed_tilesets`, `content.composed_tileset_published_snapshots`, `content.composed_tileset_publication_history` |
| Publication | `SaveAsync` avec `SaveContentIntent.Publish`. Un brouillon n’est pas dans `ListPublishedAsync` |
| Palette éditeur | `TileAssetCatalogue.MergePublishedComposedTilesets` → `ComposedTilesetPlacement.Merge`. `WorkingTileset.ServerTilesetId` vide = palette locale, conservée |

La fusion part au démarrage de l’éditeur (`MainForm`, catalogue ouvert). Fermer Données de jeu depuis le menu WPF (`MainWindow.OpenGameData`) recharge les objets de carte et ne relance pas cette fusion. `QueuePublishedComposedTilesetReload` est branché sur `MainForm.OpenGameData`, menu que `App.xaml` n’affiche pas.

## 5. CTS1 sur le client

| | |
| --- | --- |
| Format | `ComposedTilesetPackFormat.Magic` = **CTS1**, `FormatVersion` = 1, `HttpPath` = `/content/composed-tilesets` |
| Écrivain | `ComposedTilesetPackWriter.Write` — en-tête : magic, version, flags, **Hello 11**, **tuile 48**, comptes |
| Lecteur | `ComposedTilesetPackReader.Read` — refuse un `tileSizePixels` ≠ 48, le bit de chiffrement, une mauvaise clé |
| JSON brut | `IsUnprotectedCatalog` : un corps `{` ou `[` est refusé (`Catalogue brut non protégé refusé.`) |
| Serveur | `ComposedTilesetProtectedLoad.TryCreateSignedPackAsync` lit `IPublishedComposedTilesetCatalog.ListPublishedAsync`, signe avec la graine du frogpack |
| HTTP | `TilePackContentHttp.ComposedTilesetClientPath`, `HandleAsync` GET. Pas un opcode |
| Client | `ComposedTilesetClientLoader.SyncAsync` puis `Load`. Appelé à la fin de `SyncTilePackAsync`, après le frogpack |
| Journal | `Tilesets composés vérifiés : N tileset(s).` ou `Tilesets composés : ` suivi du motif |

`ComposedTilesetLoadKind` : `Loaded`, `Rejected`, `Unavailable`. Aucun tileset publié, ou service absent : `Aucun tileset composé publié.`

## Ce qui ne change pas

- `FrogWireProtocol.Version` reste **11**. CTS1 et le frogpack sont du HTTP de contenu.
- La réapparition des monstres (#147) lit `world.maps` / `world.map_published_snapshots`.`mob_spawn_zones_json` et place le monstre avec `WorldMetrics.TileCenterToPixels` (grille **32**). Ce n’est pas une seconde taille de tuile de contenu.

Fiches : [Client](client/README.md) · [Server](server/README.md) · [Editor](editor/README.md) · [Core](core/README.md) · [PostgreSQL](postgresql/README.md).

Guide auteur : [Tuiles composées](../progress/phase-10-beta-release/guides/COMPOSED_TILESETS.md). Colonnes : [modèle de données](../DATA_MODEL.md).
