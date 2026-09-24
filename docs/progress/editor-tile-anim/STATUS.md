# STATUS — Aperçu des tuiles animées (éditeur)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Prévisualiser les tuiles animées dans l’éditeur de carte, façon RPG Maker |
| **Propriétaire** | Netsun |
| **Statut** | MVP éditeur — aperçu palette + canevas |
| **Hors `.fmap`** | `MapSerializer.MapFileFormatVersion` **reste 5** — `FrogWireProtocol.Version` **reste 11** |

Le modèle carte ne stocke pas de frames d’animation. Une tuile posée garde `SrcX` / `SrcY` de la **première** case. L’aperçu décale la source dans la feuille pendant l’édition.

## Ce qui existe déjà

- Joueur / PNJ : horloges de marche, hors de ce chantier (`docs/progress/animations/`).
- Tileset : image + `TilesetId`, pas de bande animée dans `TilesetDefinition` ni dans le blob `.fmap`.
- Les anciens `animId` FCC ne sont pas mappés sur le modèle moderne.

## Livré

1. **Définition** — `AnimatedTileStrip` : origine, nombre de frames (2–8), sens horizontal (défaut) ou vertical. Durée par tileset, 200 ms par défaut.
2. **Horloge** — 3 frames : cycle **0 → 1 → 2 → 1** (eau RPG Maker). Autre nombre : boucle 0..n-1.
3. **Marquage** — sélection d’au moins 2 cases horizontales, bouton ou menu **Animer la sélection**. Le pinceau revient sur la 1re colonne. Peindre la bande entière pose l’origine, pas les frames suivantes.
4. **Aperçu** — la 1re case défile dans la palette (les frames suivantes sont assombries) et sur la carte, y compris le fantôme du pinceau. Menu **Affichage → Aperçu des tuiles animées** pour figer la 1re frame.
5. **Fichiers** — `{image}.anim.json` à côté du PNG, et `{carte}.anims.json` à l’export `.fmap` (plus `{id}.anim.json` à côté des PNG exportés). Relecture à l’ouverture. Aucun champ nouveau dans le blob carte.

## Hors scope

- Animation des tuiles dans le client / le monde en jeu.
- Bump protocole, bump `.fmap`, rips Graal, docs Exemple.
- Autotiles (bords d’eau), pas seulement le défilement de frames.

## Tests

- Linux : `Frog.Tests/AnimatedTileFrameTests.cs` (index de frame, source, canonique, JSON, version `.fmap` inchangée, libellés FR). 24 tests verts sur cette branche.
- Windows : `MapCanvasTileAnimSmokeTests` — marquage palette, peinture de l’origine, frame suivante, sidecar image. Le projet compile (`net8.0-windows`) ; l’exécution WinForms reste sur le CI Windows.
