# TileAsset, .frogpack, carte v6

Phase 1, bibliothèque `Frog.Core` seulement. L’éditeur, le client, le serveur HTTP et PostgreSQL ne changent pas dans ce lot.

## Taille de tuile

| Constante | Valeur | Rôle |
| --- | --- | --- |
| `WorldMetrics.DefaultTileSizePixels` | **32** | Défaut monde / éditeur actuel. **Ne pas le passer à 48.** |
| `TileAssetMetrics.TargetTileSizePixels` | **48** | Tuile canonique adressée par contenu. |
| En-tête `.fmap` v6 `tileSizePixels` | 48 | Taille de la carte qui référence des `TileAssetId`. |

`TileSizeMigrationPolicy` : pas d’upscale silencieux. Une feuille 32×32 ne devient pas un `TileAssetId`. Il faut ré-auteur en 48×48, ou appliquer un redimensionnement explicite dans un outil qui n’est pas le hash, puis recalculer l’id.

## TileAssetId

Empreinte **SHA-256 complète** (32 octets, 64 hex **minuscules**, pas de troncature).

Normalisation, figée :

1. Entrée : RGBA8 **row-major** (haut → bas, gauche → droite), alpha **droit**.
2. Exactement 48×48×4 = 9216 octets. Toute autre taille est refusée.
3. Prémultiplication entière : `C' = (C * A + 127) / 255` pour R, G et B, `A' = A`.
4. Le hash porte sur ces 9216 octets prémultipliés.

L’id ne dépend pas de la position dans la feuille. L’id tout à zéro est réservé (`TileAssetId.None` = pas de graphique).

`TileSheetSlicer.Slice` : grille `floor(largeur / 48) × floor(hauteur / 48)`. Le reliquat de pixels (bord droit et bas) est ignoré. Les cellules identiques partagent un seul `TileAsset`.

## Carte `.fmap`

| Version | Lecture | Écriture | Graphique d’une cellule |
| --- | --- | --- | --- |
| v3, v4 | oui | non (héritage) | `TilesetId`, `SrcX`, `SrcY` |
| v5 | oui | `MapSerializer.Serialize` si `GraphicIdentity = SheetSource` (défaut) | idem v5, pas de `TileAssetId` |
| v6 | oui | `MapFormat.Write` / `Serialize` si `GraphicIdentity = TileAsset` | `TileAssetId` (32 octets). Pas de Src. |

En-tête v6, après l’octet d’options v4/v5 : `tileSizePixels` (Int32 LE), obligatoirement 48.

Couches inchangées (type, visible, verrou, nom, tuiles). Warp Guid et script restent sur la cellule. Les événements de carte ne sont pas dans ce blob : leurs stockages actuels restent la source.

Pas de mode mixte Src+Id : `Map.Validate` refuse une tuile qui porterait les deux, et refuse un `TileAssetId` sur une carte v5. Les cartes feuille continuent d’être écrites en v5 pour ne pas perdre les coordonnées de l’éditeur (undo, export, blobs déjà en base). Il n’y a pas de conversion automatique v5 → v6.

`FrogWireProtocol.Version` reste **11**. Ce format de fichier n’est pas le Hello TCP.

## `.frogpack` (V1, paquet complet, pas de delta)

Magic `FPK1`, little-endian.

```
u16 version = 1
u16 flags = 0          bit 0 = chiffrement, interdit en V1 ; tout autre bit est refusé
u16 tileSizePixels = 48
u16 reserved = 0
u32 tileCount
entrée × tileCount, triées par TileAssetId :
    id 32 | offset u64 | size u32 | sha256 blob 32
blobs : RGBA prémultiplié 48×48, size = 9216, offset = index × 9216
sha256 du corps (en-tête + manifeste + blobs) 32
clé publique Ed25519 32
signature Ed25519 64 sur ce sha256 (pas sur le fichier entier)
```

`FrogPackReader.Read` exige la clé publique de confiance. Il refuse, sans renvoyer de tuiles : magic, version, flags (chiffrement inclus), taille ≠ 48, SHA-256 du corps, clé différente, signature invalide, hash de blob, manifeste non trié, longueur inattendue.

## Modèles hors pixels

- `WorkingTileset` : nom + liste ordonnée de `TileAssetId`. Aucune grille de pixels. La carte ne stocke pas la position dans la palette. Le canevas est l’UI éditeur (phase suivante).
- `TileAnimation` : frames = `TileAssetId` ordonnés + durée. L’index réutilise `AnimatedTileFrames` (ping-pong à 3 frames). Les sidecars `.anim.json` restent en place.

`ITileAssetLookup` / `MemoryTileAssetLookup` : point d’accroche du cache client et du dépôt serveur.

## Phases suivantes (pas dans ce PR)

1. **Éditeur** — import de feuille via `TileSheetSlicer`, canevas `WorkingTileset`, enregistrement `MapFormat.Write` une fois les cellules en `TileAssetId`.
2. **Serveur** — publication d’un `.frogpack` vérifié avec une clé Ed25519 épinglée. Pas d’endpoint HTTP ici.
3. **Client** — téléchargement et cache par `TileAssetId`. Pas d’UI ici.
4. **PostgreSQL** — `world.map_cells.layers_json` reste `tilesetId` / `srcX` / `srcY`. Pas de DDL ici.
5. **Contenu** — les cartes existantes restent v5 / 32 px jusqu’à ré-auteur ou upscale explicite.
