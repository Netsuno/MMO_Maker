# STATUS — Client player skin v2 (chibi 32×32)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Remplacer le 16×16 pauvre par un skin chibi top-down natif 32×32 |
| **Propriétaire** | Netsun |
| **Statut** | Paperdoll : corps + tête Eldiran, overlays originaux tunique / armure / casque / arme |
| **Base** | `main` @ `3dbf63f` (paperdoll MVP déjà fusionné, PR #49) |
| **Branche** | `cursor/paperdoll-tunic-overlay-53d8` |
| **PR** | Draft [#50](https://github.com/Netsuno/MMO_Maker/pull/50) vers `main` — **pas de merge** |

Protocole / gameplay / `WorldMetrics.DefaultTileSizePixels = 32` inchangés.
Affichage nearest-neighbor seulement (pas de bicubique / ColorMatrix or).

## Couches

Draw order : **body → tunic → armor → head → hat → weapon** (`PlayerSpriteSlot` / `PaperdollDrawOrder`).
Même ordre sur chaque cellule de marche (l'arme reste devant, y compris vers le nord : la lame est à côté du corps).

| Slot | MVP | Fichier |
| --- | --- | --- |
| Body | Eldiran torso/jambes | `player-body.png` + `player-walk-body.png` |
| Tunic | toile ocre originale si tunique locale | `player-tunic.png` + `player-walk-tunic.png` |
| Armor | overlay original si `EquippedArmorItemId` | `player-armor.png` + `player-walk-armor.png` |
| Head | Eldiran visage (rows 0–13) | `player-head.png` + `player-walk-head.png` |
| Hat | casque original, **après** la tête | `player-hat.png` + `player-walk-hat.png` |
| Weapon | overlay original si `EquippedWeaponItemId` | `player-weapon.png` + `player-walk-weapon.png` |

`player.png` = idle sud déjà composé (secours). Pas de sheet Graal embarquée.
Overlays régénérés par `python3 tools/generate-paperdoll-overlays.py` (procédural, pas Eldiran, pas Graal).

## Paperdoll MVP

- Arme et armure suivent le snapshot inventaire déjà en place (`EquipmentSlot` = `EquipmentSlotKind` 1 / 2). Pas de bump protocole.
- Casque : bouton **Porter le casque** / **Retirer le casque** dans le panneau équipement. Identifiant local, jamais envoyé. Slot `Headwear` sans champ fil.
- Tunique : bouton **Porter la tunique** / **Retirer la tunique**. Identifiant local (`LocalTunicItemId`), jamais envoyé. Slot `Tunic` sans champ fil. La toile est plus large que la plaque verte ; l'armure est dessinée par-dessus et l'ourlet reste visible.
- `FrogWireProtocol.Version` reste 11. Pas de bump protocole.
- Main gauche / bouclier (`Offhand`) : enum seulement, pas de sprite.
- Joueur local : `MainShellForm` passe `localAppearance` à `MapViewRenderer`. Déséquipé → overlays cachés, corps + tête restent.
- **Écart :** les autres joueurs ne reçoivent pas l'équipement (`PositionUpdate` n'a pas ces champs). Ils restent corps + tête. Le casque et la tunique non plus ne sont pas réseau.

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
