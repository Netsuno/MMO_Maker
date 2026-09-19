# STATUS — Client hide world grid

| Champ | Valeur |
| --- | --- |
| **Chantier** | Retirer la grille tuile du GameWorldView (vue monde Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | Grille debug défaut **off** — pas de filets visibles en jeu |
| **Base** | `main` @ `eb731de` (merge PR #11 Kenney pack) |
| **Branche** | `cursor/client-hide-world-grid-ee05` |
| **PR** | Draft vers `main` — **pas de merge** |

`MapViewRenderer` dessinait un `DrawRectangle` semi-transparent sur **chaque** tuile (`Pen` ARGB 40,0,0,0). C’était un overlay debug, pas un tileset.

Jeu : `MainShellForm` appelle `Render` **sans** `showTileGrid` → défaut `false`.
Tests : `showTileGrid: true` reste disponible ; overlay debug = filets 1 px alignés (pas `DrawRectangle` + Half, qui ratait les coutures en smoke Windows).

Inchangé : tilesets, caméra / `_smoothTimer` 16 ms, HUD, sprites joueurs, marqueurs d’événements carte.
