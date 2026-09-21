# STATUS — Prefab map objects (publish → client)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Objets monde réutilisables (prefabs) pour construction carte / maison |
| **Propriétaire** | Netsun |
| **Statut** | Catalogue publié additif `prefabs` / `prefabMaps` + matérialisation client (même famille que tilesets #38) |
| **Base** | `main` @ `4d0e30f` (merge PR #38 tileset PNG bytes) |
| **Branche** | `cursor/fix-published-prefabs-client-67c4` |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `MapSerializer.MapFileFormatVersion` **reste 5** |

Import projet : `ProjectAssetImporter` / `ProjectAssetKind.Prefabs` (`prefabs/`) étend #14. Catalogue publié additif `prefabs` / `prefabMaps` + `pngBase64`.

---

## Ce qui marche

1. **Modèle** — `PrefabDefinition` : `id`, empreinte tuiles **et/ou** pixels, variantes de facing. Catalogue in-repo `assets/prefabs/catalog.json` : `sofa`, `fence-post`, `fence-rail`, `bed`, `table`, `chair`, `plant`, `chest`.
2. **Placeholders originaux** — `tools/generate-prefab-placeholders.py` écrit des PNG procéduraux CC0-style (stdlib zlib, aucun téléchargement). Copiés vers `Prefabs/` côté éditeur et client.
3. **Éditeur** — `MapCanvas` outil `EditorTool.Prefab` (touche **P**), palette objet + facing, clic / glisser = pose, clic droit = gomme. **Pipette** : **I** ou **Alt+clic**. **Glisser** une instance posée pour la déplacer. Ghost empreinte. `[` / `]` cycle le facing.
4. **Persistance sans bump** — sidecar `{carte}.prefabs.json` à l’export + mémo `editor-workstate.json`. **Publier (PostgreSQL)** écrit `prefabs_json` (jsonb) sur `maps` et `map_published_snapshots` (catalogue + placements + PNG base64). Playtest : `Prefabs/` + `Maps/*.prefabs.json` **et** sidecar additif `published-prefabs.json` à côté du manifeste. Réenregistrement fusionne le catalogue/PNG persistés si le `Prefabs/` local ne les a pas. Éditeur : placements dirty (prompt de sauvegarde) ; workstate non enregistré primé à la restauration.
5. **Catalogue fil additif** — `PublishedCatalogResult` JSON : champs optionnels `prefabs` (`id`, variantes, `pngBase64`) et `prefabMaps` (`mapId`, `mapName`, `runtimeMapId` optionnel, placements). Pas de bump `FrogWireProtocol`.
6. **Client** — à la réception du catalogue (et au `MapData`), `ClientPublishedPrefabMaterializer` écrit `Prefabs/` + `Maps/{nom}.{mapId}.prefabs.json` (alias `{nom}.prefabs.json` si le nom est unique) dans exe **et** cwd, puis `ClientPrefabLoader` + `MapViewRenderer.DrawPlacedPrefabs`. Pas de copie manuelle de `Prefabs/`.

---

## Différé (volontaire)

- Animation / IA / vente de packs.
- Undo `.fmap` des instances (hors blob, comme le spawn).
- Collision / assise / multi-étage.
- Éditeur Game Data dédié aux prefabs (le paquet voyage avec la carte publiée).

---

## Hors scope volontaire

- Aucun asset tiers de MMORPG classique.
- Pas de bump fil ni `.fmap`.
- Pas de second système de cartes.

---

## Tests

- Linux : `PrefabModelAndPlacementTests` (modèle, pipette/move, sidecar, protocole inchangé), `ClientPrefabDrawTests` (IHDR PNG, catalogue, chemin de dessin, STATUS), `PublishedPrefabClientCoverageTests` + `PublishedCatalogPrefabsTests` (échec si placement sans image matérialisable).
- PostgreSQL : `PostgresMapRepositoryTests.Publish_StoresPrefabPackage_CatalogCoversPlacements`.
- Windows smoke : pose / pipette / glisser-déplacer.

## Repro live

1. Éditeur : outil **P**, poser un canapé / une clôture.
2. **Publier (PostgreSQL)**.
3. Serveur live (catalogue publié) → client se connecte (exe+cwd, **sans** copier `Prefabs/` à la main).
4. `PublishedCatalogResult` livre `prefabs` + `prefabMaps` ; le client matérialise les PNG puis `DrawPlacedPrefabs`.
