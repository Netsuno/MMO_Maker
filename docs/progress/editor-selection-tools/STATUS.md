# STATUS — Éditeur : copier-coller de sélection multi-couches

| Champ | Valeur |
| --- | --- |
| **Chantier** | Sélection (M) : copier / couper / coller le rectangle sur toutes les couches |
| **Propriétaire** | Netsun |
| **Statut** | Livré — PR ready-for-review, rebasé sur `main` |
| **Base** | `main` @ `00050b5` (tuiles animées #52) |
| **Branche** | `cursor/editor-multilayer-selection-copy-1081` |
| **PR** | [#53](https://github.com/Netsuno/MMO_Maker/pull/53) vers `main` |
| **Protocole** | Chantier #53 : `FrogWireProtocol.Version` **reste 11** — `MapSerializer.MapFileFormatVersion` **reste 5** (cartes feuille). Au tip d’inventaire `8aec6e64`, Hello **reste 11** ; une carte `TileAsset` s’écrit en `.fmap` **v6** (tuiles **48×48**). Le v5 reste le chemin `SheetSource` |

Parallèle aux chantiers déjà livrés (rotation / miroir / pipette, spawn, prefabs) : **aucun** bump `.fmap`, **aucun** changement protocole, client, ou publication prefab.

---

## Livré

1. **Ctrl+C / Ctrl+X / Ctrl+V** capturent et restituent le rectangle sur **toutes les couches** de la carte (sol, frange, attributs, et toute autre couche du modèle). Le collage réécrit aussi les cases vides du rectangle (trous). **Une seule entrée d’annulation** pour le collage, la coupe, la suppression et la rotation in situ.
2. **Ctrl+Maj+C / X / V** et **Maj+Q / H / V** limitent l’opération à la **couche active** (l’ancien comportement). Sans sélection, Q/H/V tournent ou miroitent tout le presse-papiers, dont toutes les couches qu’il contient.
3. **Attributs déjà présents** sur la tuile (`BlockAttribute`, `WarpAttribute`, `ResourceAttribute`) sont copiés en mémoire avec le type, le tileset, la destination de warp et le script. Les couches verrouillées sont copiées mais ni écrasées, ni tournées, ni coupées. Le format v5 conserve `TileType`, warp et script ; on n’ajoute pas de version pour la liste d’attributs (déjà absente du `.fmap`).
4. Libellés FR : menu Édition (WPF et WinForms), barre d’état, hint de l’outil Sélection. Raccourcis Ctrl+C/X/V relayés depuis la coque WPF.

La rotation 90° (Q), les miroirs H/V et la pipette (I) restent en place. Suppr efface le rectangle sur toutes les couches éditables ; Maj+Suppr n’efface que la couche active.

---

## Gestes canevas (#136)

À côté du copier-coller clavier ci-dessus. Code : `MapCanvas.TryBeginSelectionDrag`, `TryRelocateSelection`, `FinishDoubleClickStamp`.

1. Clic gauche glissé **dans** le rectangle **déplace**. Clic droit glissé **copie**. Un pas d’annulation chacun.
2. Tampon privé : Ctrl+C n’est pas touché. L’origine du déplacement est effacée puis recollée (chevauchement sûr). La sélection suit la copie.
3. Les numéros de région restent en place.
4. Maj = couche active, comme Ctrl+Maj sur le presse-papiers.
5. Un glisser **hors** sélection retrace le rectangle. Un clic droit hors sélection efface la sélection.
6. Double-clic : pipette sans changer d’outil. Le premier demi-clic (pinceau, pot, rectangle, ligne) est annulé, puis la pipette pose le tampon.

Le guide auteur reprend ces gestes : [`../phase-10-beta-release/guides/CREATOR_QUICKSTART.md`](../phase-10-beta-release/guides/CREATOR_QUICKSTART.md). Disposition : [`../../EDITOR_WORKSPACE.md`](../../EDITOR_WORKSPACE.md).

---

## Hors scope

- Prefabs, déplacement d’événements, bump protocole ou format, changements client.
- Persistance nouvelle de `ResourceAttribute.ResourceId` (déjà absente du `.fmap` v5 et du JSON cellule).

---

## Déjà en place (ne pas régresser)

Rotation 90°, miroirs H/V, pipette I — voir l’historique PR [#39](https://github.com/Netsuno/MMO_Maker/pull/39). Raccourcis sans modificateur : ne volent pas Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+Z.

Aperçu des tuiles animées ([#52](https://github.com/Netsuno/MMO_Maker/pull/52)) : les outils pinceau, pot, rectangle et ligne gardent le suffixe « tuile animée » dans la barre d’état. Les phrases de sélection (geste en cours et rectangle figé) restent prioritaires.

---

## Tests

- Linux / unitaires : `Frog.Tests/EditorSelectionToolsTests.cs` (rectangle multi-couches, trous, attributs, couche verrouillée, rotation alignée, format v5) et copie d’attributs dans `MapEditOperationsTests`.
- Windows smoke : `MapCanvasSelectionPipetteSmokeTests` (collage toutes couches, un seul undo/redo, coupe, Ctrl+Maj couche active, Maj+Q) et hints `EditorToolHotkeysTests` (« toutes les couches », comparaison ordinale insensible à la casse).
