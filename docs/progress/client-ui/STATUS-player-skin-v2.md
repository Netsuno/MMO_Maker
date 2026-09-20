# STATUS — Client player skin v2 (chibi 32×32)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Remplacer le 16×16 pauvre par un skin chibi top-down natif 32×32 |
| **Propriétaire** | Netsun |
| **Statut** | Native 32×32 Eldiran CC0, nearest ×1, pieds sur `(Cx,Cy)` — pas de resize monde |
| **Base** | `main` @ `60757c9` (merge PR #16 contrast, includes #15 skin) |
| **Branche** | `cursor/client-player-skin-v2-5d89` |
| **PR** | Draft vers `main` — **pas de merge** |

Protocole / gameplay / `WorldMetrics.DefaultTileSizePixels = 32` inchangés.
Affichage nearest-neighbor seulement (pas de bicubique / ColorMatrix or).

## Couches (équipement plus tard)

Draw order : **body → tunic → armor → head → weapon** (`PlayerSpriteSlot`).

| Slot | v1 | Fichier |
| --- | --- | --- |
| Body | Eldiran torso/jambes (rows 14–31) | `player-body.png` |
| Head | Eldiran casque/visage (rows 0–13) | `player-head.png` |
| Tunic / Armor / Weapon | vides — overlay équipement plus tard | — |

`player.png` = idle sud déjà composé (secours). Pas de sheet Graal embarquée.

---

## Avant → après

| Item | v1 (PR #15) | v2 |
| --- | --- | --- |
| Asset | original 16×16 in-repo | Eldiran CC0 32×32 cell (col 1, row 4, south stand) |
| Display | nearest ×2 → 32 px | nearest ×1 → 32 px (1 tile wide) |
| Ancrage | centre sprite sur `(Cx,Cy)` | pieds / centre bas sur `(Cx,Cy)` |
| Look | cape/tunique olive micropixel | chevalier bleu chibi ~2 têtes, contour sombre 1 px |
| Autres joueurs | teinte froide (OK) | même teinte froide (pas ellipse or) |

---

## Hors scope

- Walk 4 dirs — see [../animations/STATUS.md](../animations/STATUS.md)
- Remonter `tileSize` / résolution monde
- Rip moodboard / Graal
- Re-ship Kenney 16×16

Linux / cet agent : pas de capture WinForms live. Revue pixel = PNG natif + composite
nearest sur herbe FRoG + smokes Windows CI (`MapViewRendererSmokeTests`).
