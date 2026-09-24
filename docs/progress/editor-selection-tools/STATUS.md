# STATUS — Éditeur : copier-coller de sélection multi-couches

| Champ | Valeur |
| --- | --- |
| **Chantier** | Sélection (M) : copier / couper / coller le rectangle sur toutes les couches |
| **Propriétaire** | Netsun |
| **Statut** | Livré — PR ready-for-review |
| **Base** | `main` @ `3dbf63f` |
| **Branche** | `cursor/editor-multilayer-selection-copy-1081` |
| **PR** | [#53](https://github.com/Netsuno/MMO_Maker/pull/53) vers `main` |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `MapSerializer.MapFileFormatVersion` **reste 5** |

Parallèle aux chantiers déjà livrés (rotation / miroir / pipette, spawn, prefabs) : **aucun** bump `.fmap`, **aucun** changement protocole, client, ou publication prefab.

---

## Livré

1. **Ctrl+C / Ctrl+X / Ctrl+V** capturent et restituent le rectangle sur **toutes les couches** de la carte (sol, frange, attributs, et toute autre couche du modèle). Le collage réécrit aussi les cases vides du rectangle (trous). **Une seule entrée d’annulation** pour le collage, la coupe, la suppression et la rotation in situ.
2. **Ctrl+Maj+C / X / V** et **Maj+Q / H / V** limitent l’opération à la **couche active** (l’ancien comportement). Sans sélection, Q/H/V tournent ou miroitent tout le presse-papiers, dont toutes les couches qu’il contient.
3. **Attributs déjà présents** sur la tuile (`BlockAttribute`, `WarpAttribute`, `ResourceAttribute`) sont copiés en mémoire avec le type, le tileset, la destination de warp et le script. Les couches verrouillées sont copiées mais ni écrasées, ni tournées, ni coupées. Le format v5 conserve `TileType`, warp et script ; on n’ajoute pas de version pour la liste d’attributs (déjà absente du `.fmap`).
4. Libellés FR : menu Édition (WPF et WinForms), barre d’état, hint de l’outil Sélection. Raccourcis Ctrl+C/X/V relayés depuis la coque WPF.

La rotation 90° (Q), les miroirs H/V et la pipette (I) restent en place. Suppr efface le rectangle sur toutes les couches éditables ; Maj+Suppr n’efface que la couche active.

---

## Hors scope

- Prefabs, déplacement d’événements, bump protocole ou format, changements client.
- Persistance nouvelle de `ResourceAttribute.ResourceId` (déjà absente du `.fmap` v5 et du JSON cellule).

---

## Déjà en place (ne pas régresser)

Rotation 90°, miroirs H/V, pipette I — voir l’historique PR [#39](https://github.com/Netsuno/MMO_Maker/pull/39). Raccourcis sans modificateur : ne volent pas Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+Z.

---

## Tests

- Linux / unitaires : `Frog.Tests/EditorSelectionToolsTests.cs` (rectangle multi-couches, trous, attributs, couche verrouillée, rotation alignée, format v5) et copie d’attributs dans `MapEditOperationsTests`.
- Windows smoke : `MapCanvasSelectionPipetteSmokeTests` (collage toutes couches, un seul undo/redo, coupe, Ctrl+Maj couche active, Maj+Q) et hints `EditorToolHotkeysTests`.
