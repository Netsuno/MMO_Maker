# STATUS — Prefab map objects MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Objets monde réutilisables (prefabs) pour construction carte / maison |
| **Propriétaire** | Netsun |
| **Statut** | MVP branché sur `ProjectAssetImporter`, sidecar tilesets, `MapCanvas`, `MapViewRenderer` — **pas de merge** |
| **Base** | `main` @ `b9bbaa7` (merge PR #25 walk anim) |
| **Branche** | `cursor/editor-prefab-objects-mvp` |
| **PR** | Draft [#26](https://github.com/Netsuno/MMO_Maker/pull/26) vers `main` — **pas de merge** |
| **Tip** | `21097cc5e75e56d19f39d853bd01d653d20f1c3c` |
| **CI** | [35530552118](https://github.com/Netsuno/MMO_Maker/actions/runs/35530552118) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `MapSerializer.MapFileFormatVersion` **reste 5** |

Parallèle à #14 (pipeline assets) et #22 (spawn workstate). Animation joueur (#25) **non touchée**.

---

## Ce qui marche (MVP)

1. **Modèle** — `PrefabDefinition` : `id`, empreinte tuiles **et/ou** pixels, variantes de facing optionnelles. Catalogue in-repo `assets/prefabs/catalog.json` : `sofa` (2×1 sud/nord, 1×2 est/ouest), `fence-post`, `fence-rail` (kit segments).
2. **Placeholders originaux** — `tools/generate-prefab-placeholders.py` écrit des PNG procéduraux CC0-style (stdlib zlib, aucun téléchargement). Copiés vers `Prefabs/` côté éditeur et client.
3. **Éditeur** — outil `EditorTool.Prefab` (touche **P**), palette objet + facing, clic / glisser comme le pinceau, clic droit = gomme d’instance (pas de peinture tuile). Ghost empreinte. `[` / `]` cycle le facing.
4. **Persistance sans bump** — sidecar additif `{carte}.prefabs.json` à l’export (même famille que `.tilesets.json`) + mémo `mapPrefabPlacements` dans `editor-workstate.json`. Playtest (`EditorPlaytestTilesetSidecar`) écrit `Prefabs/` + `Maps/*.prefabs.json`.
5. **Client** — `ClientPrefabLoader` (layout `ClientTilesetLoader`) + `MapViewRenderer.DrawPlacedPrefabs` après les tuiles, avant joueurs. Statique uniquement.

Import projet : `ProjectAssetKind.Prefabs` (`prefabs/`) étend #14.

---

## Différé (volontaire)

- Animation / IA / vente de packs.
- Champ catalogue publié `prefabs` (les tilesets ont déjà un champ additif ; pas requis pour playtest fichier).
- Undo `.fmap` des instances (hors blob, comme le spawn).
- Collision / assise / multi-étage.
- Pipette, déplacement curseur d’objets.

---

## Hors scope volontaire

- Aucun asset tiers de MMORPG classique.
- Pas de bump fil ni `.fmap`.
- Pas de second système de cartes.

---

## Tests

- Linux : `Frog.Tests/PrefabModelAndPlacementTests.cs` (modèle, placement, sidecar, protocole inchangé) + `ClientPrefabDrawTests.cs` (IHDR PNG, catalogue, chemin de dessin, STATUS).
- Windows smoke : `MapCanvasPrefabSmokeTests` (pose sans peinture tuile) + raccourci **P**.

CI **green** on `21097cc` : [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35530552118/job/106130289461) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35530552118/job/106130289323). Windows editor / gameplay / Phase 8 smokes included.
