# Publication serveur `.frogpack` V1

Canal **additif**. Le Hello TCP reste **`FrogWireProtocol.Version` = 11**. `WorldMetrics.DefaultTileSizePixels` reste **32**. Les tuiles du paquet sont en **48×48** (`TileAssetMetrics.TargetTileSizePixels`).

Format du fichier : [`Frog.Core/Docs/tile-assets-frogpack-map-v6.md`](../../Frog.Core/Docs/tile-assets-frogpack-map-v6.md) (magic `FPK1`, paquet complet, pas de delta). Le bit de chiffrement est refusé par `FrogPackReader`.

La publication **ne consulte pas** `player.player_tile_unlocks`. Les déblocages éditeur ne filtrent pas le paquet serveur.

Les tables viennent de la migration `20260924214100_TileAssetCatalog` : `content.tiles`, `content.tile_packs`, `content.tile_pack_entries`. `content.tilesets` n’est pas modifié. Un paquet publié ou retiré est immuable (déclencheur PostgreSQL). Le seul passage de statut autorisé ensuite est publié (1) → retiré (2).

`content.tiles.png_bytes` reçoit, pour une publication serveur V1, les **9216 octets RGBA prémultipliés** dont le SHA-256 est le `TileAssetId` (`TilePackPixelEncoding`). `meta_json` porte `pixelEncoding=premultiplied-rgba8`.

## Clé Ed25519 épinglée

La clé **publique** est la confiance. La graine **privée** ne sert qu’à signer un paquet produit ici. Un fichier déjà signé est accepté seulement si `FrogPackReader` le valide avec la clé épinglée — il n’est pas re-signé.

Générer une paire de test (ne pas committer la graine) :

```bash
dotnet run --project Frog.Server -- --tilepack-keygen
```

Sortie :

```text
FROG_TILEPACK_PUBLIC_KEY_HEX=<64 hex>
FROG_TILEPACK_PRIVATE_SEED_HEX=<64 hex>
```

La graine est celle de `FrogPackKeys` (32 octets), pas une clé étendue. Équivalent manuel : `FrogPackKeys.Generate()`, puis `Convert.ToHexString(...).ToLowerInvariant()`.

Configuration (les variables d’environnement écrasent le JSON) :

| Réglage | Variable | Rôle |
| --- | --- | --- |
| `TilePack:PublicKeyHex` | `FROG_TILEPACK_PUBLIC_KEY_HEX` | Clé publique épinglée, 32 octets hex. Requis pour publier. |
| `TilePack:PrivateSeedHex` | `FROG_TILEPACK_PRIVATE_SEED_HEX` | Graine de signature. Requis pour assembler des tuiles. Pas requis pour un upload déjà signé. |
| `TilePack:AdminToken` | `FROG_TILEPACK_ADMIN_TOKEN` | Jeton du `POST`. Vide : le `POST` HTTP est refusé. |
| `TilePack:Enabled` | `FROG_TILEPACK_CONTENT_ENABLED` | `true` ouvre le canal HTTP. Défaut **false** (aucun port au démarrage du jeu). |
| `TilePack:Port` | `FROG_TILEPACK_CONTENT_PORT` | Défaut 6080, distinct du port TCP 6000. |
| `TilePack:BindAddress` | — | Défaut `127.0.0.1`. Hors loopback : `AllowNonLoopbackContentBind=true`. |
| `TilePack:PngImportRoot` | `FROG_TILEPACK_PNG_IMPORT_ROOT` | Racine des dossiers PNG via HTTP. |

Si la graine et la clé publique sont toutes les deux renseignées et ne correspondent pas, le processus refuse de démarrer.

## Publier

L’assemblage passe par `FrogPackWriter`, puis **`FrogPackReader` avec la clé épinglée**. Tant que cette lecture échoue (mauvaise signature, mauvais hash, chiffrement, taille ≠ 48), aucune ligne n’est marquée publiée.

PostgreSQL est requis pour une publication qui doit survivre au processus (`PostgreSql:Enabled=true`). Le dépôt mémoire ne sert qu’aux tests et au repli hors base.

Crochet sans ouvrir le socket de jeu :

```bash
dotnet run --project Frog.Server -- --tilepack-publish --slug world --version 1 --folder ./tiles
dotnet run --project Frog.Server -- --tilepack-publish --slug world --version 1 --frogpack ./world.frogpack
dotnet run --project Frog.Server -- --tilepack-publish --slug world --version 2 --from-catalogue
```

`--folder` lit les `*.png` du dossier (pas les sous-dossiers). Chaque fichier est un PNG 8 bits RGB ou RGBA, non entrelacé. Une image 48×48 est une tuile. Une image plus grande est découpée par `TileSheetSlicer` (grille entière, reliquat ignoré, pas d’upscale). Une image plus petite que 48×48 est refusée.

`--from-catalogue` republique **toutes** les lignes de `content.tiles`, sans filtre de déblocage.

HTTP d’administration (en-tête `X-Frog-TilePack-Admin: <jeton>`), une fois `TilePack:Enabled=true` :

- `POST /content/tile-packs?slug=world&version=1` avec `Content-Type: application/octet-stream` et le fichier `.frogpack`.
- `POST /content/tile-packs` JSON `{ "slug", "version", "tiles": [ { "rgbaBase64", "displayName" } ] }` — RGBA **droit** 48×48×4.
- `POST /content/tile-packs` JSON `{ "slug", "version", "folder": "sous-dossier" }` relatif à `PngImportRoot`.
- `POST /content/tile-packs` JSON `{ "slug", "version", "fromCatalogue": true }`.
- `POST /content/tile-packs/yank` JSON `{ "slug", "version" }`.

`slug` : 1–120 caractères, `[a-z0-9][a-z0-9._-]*`. `version` : 1–64, `[A-Za-z0-9][A-Za-z0-9._+-]*`. Le couple est unique. Deux paquets **publiés** ne peuvent pas partager le même SHA-256 de corps. Retirer l’ancien avant de republier les mêmes octets.

## Téléchargement client

Pas d’opcode. Le client (phase suivante) interroge HTTP :

- `GET /content/tile-packs/current?slug=world` — JSON : `version`, `tileCount`, `tileSizePixels` (48), `frogpackSha256` (SHA-256 du **corps** FPK1, celui couvert par la signature), `ed25519Signature`, `ed25519PublicKeyId`, `downloadPath`, `protocolVersion` (11).
- `GET /content/tile-packs/current.frogpack?slug=world` — octets du paquet. `ETag` = le sha256. `If-None-Match` peut répondre 304.

Sans `slug`, le paquet publié le plus récent (`published_at_utc`) est renvoyé. Un paquet retiré n’est plus « courant ». Les chemins tileset / prefab (`PublishedCatalog`) ne changent pas.
