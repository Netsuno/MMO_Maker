# STATUS — Éditeur : rotation / miroir de sélection + pipette tuile

| Champ | Valeur |
| --- | --- |
| **Chantier** | Outil Sélection (M) : rotation 90° + miroirs ; pipette pinceau |
| **Propriétaire** | Netsun |
| **Statut** | Livré — PR ready-for-review |
| **Base** | `main` @ `4d0e30f` |
| **Branche** | `cursor/editor-selection-rotate-pipette-b4b5` |
| **PR** | [#39](https://github.com/Netsuno/MMO_Maker/pull/39) vers `main` |
| **Tip** | `ac936b9` |
| **CI** | [35592864093](https://github.com/Netsuno/MMO_Maker/actions/runs/35592864093) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `MapSerializer.MapFileFormatVersion` **reste 5** |

Parallèle au chantier spawn (#22) : **aucun** changement publish prefab / PostgreSQL / catalogue / matérialisation client. Pas de bump `.fmap`.

---

## Livré

1. **Rotation 90° horaire (Q)** — si un rectangle de sélection est commis sur la couche active : transforme les tuiles **in situ** (annulable) et met à jour le presse-papiers éditeur. Sinon, transforme uniquement le tampon copier/coller (`Ctrl+C` / `Ctrl+V`).
2. **Miroir horizontal (H)** et **miroir vertical (V)** — même règle (sélection in situ, sinon presse-papiers). Les `SrcX` / `SrcY` du tileset ne tournent pas : on déplace les tuiles, on ne pivot pas les pixels d’atlas.
3. **Pipette (I)** — échantillonne `TilesetId` + `SrcX`/`SrcY` + type depuis la tuile sous le curseur (couche active, sinon couche visible du dessus). Passe au **Pinceau** et synchronise la palette. **Alt+clic** : même échantillon **sans** changer d’outil (pas de peinture).
4. UI FR : menus Édition / Carte, bouton barre **Pipette**, hints palette.

Raccourcis **sans** modificateur : ne volent pas `Ctrl+C` / `Ctrl+X` / `Ctrl+V` / `Ctrl+Z`.

---

## Hors scope (volontaire)

- Prefabs publish / PostgreSQL / catalogue / `PrefabDefinition` / `ClientPrefabLoader` / dessin prefab `MapViewRenderer` / `MapPublishedTilesetSync`.
- Ligne, cercle, tampon multi-cartes, déplacement d’objets au curseur.
- Rotation antihoraire (un seul sens 90° horaire).

---

## Tests

- Linux / unitaires : `Frog.Tests/EditorSelectionToolsTests.cs` (maths rotation/miroir, presse-papiers, in situ, pipette couches, protocole inchangé).
- Windows smoke : `MapCanvasSelectionPipetteSmokeTests` + raccourcis `EditorToolHotkeysTests` (I/Q/H/V ne sont pas des outils).

CI **SUCCESS** sur `ac936b9` : [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35592864093/job/106311040598) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35592864093/job/106311040346). Windows editor smokes inclus dans `build-and-test`.
