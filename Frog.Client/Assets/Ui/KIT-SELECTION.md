# Kit UI — sélection concrète (packs existants)

Source Kenney : [UI Pack RPG Expansion](https://www.kenney.nl/assets/ui-pack-rpg-expansion) via [OGA mirror](https://opengameart.org/content/ui-pack-rpg-extension) — **CC0** (crédit apprécié).  
Zip local vérifié : `UIpack_RPG.zip` / dossiers `PNG/`.

## Cible dépôt

```
Frog.Client/Assets/Ui/
  frames/
  slots/
  bars/
  menu/
  hotbar/
  THIRD_PARTY.md
```

## Mapping Kenney → Assets

| Cible | Fichier source (Kenney PNG/) | Usage |
| --- | --- | --- |
| `frames/panel.png` | `panel_brown.png` | Chrome fenêtre (teinte code vers `#161C28` / or) |
| `frames/panel_inset.png` | `panelInset_brown.png` | Zones intérieures |
| `slots/slot.png` | `buttonSquare_brown.png` | Case inventaire / hotbar vide |
| `slots/slot_pressed.png` | `buttonSquare_brown_pressed.png` | Pressé / sélection |
| `menu/btn_round.png` | `buttonRound_brown.png` | Pastille menu BD |
| `bars/hp_*.png` | `barRed_horizontal{Left,Mid,Right}.png` | HP 9-slice |
| `bars/mp_*.png` | `barBlue_horizontal{Left,Mid,Right}.png` | MP |
| `bars/track_*.png` | `barBack_horizontal{Left,Mid,Right}.png` | Piste |
| `bars/xp_*.png` | `barYellow_horizontal{Left,Mid,Right}.png` | XP si un jour max filaire |
| `chrome/btn_long.png` | `buttonLong_brown.png` (+ `_pressed`) | CTA Options / dialogue |
| `chrome/arrow_*.png` | `arrowBrown_{left,right}.png` | Choix dialogue |

## Icônes menu + hotbar (game-icons.net — **CC-BY**, crédit obligatoire)

Télécharger PNG mono (ex. or `#C9A227` sur fond transparent) :

| Cible | Icône game-icons (nom) |
| --- | --- |
| `menu/icon_perso.png` | `walk` ou `character` |
| `menu/icon_inv.png` | `backpack` |
| `menu/icon_quetes.png` | `scroll-unfurled` |
| `menu/icon_carte.png` | `treasure-map` |
| `menu/icon_options.png` | `cog` |
| `hotbar/icon_melee.png` | `broadsword` |
| `hotbar/icon_spell.png` | `fire-spell-cast` |
| `hotbar/icon_interact.png` | `hand` |

Lien catalogue : https://game-icons.net/

## THIRD_PARTY.md (à coller)

- Kenney Vleugels — UI Pack RPG Expansion — CC0 — www.kenney.nl  
- Icons by Game-icons.net contributors — CC BY 3.0 — https://game-icons.net/

## Note DA

Le pack Kenney est **beige/brun fantasy**, pas sombre/or natif : le client garde `UiTheme` (`#161C28` / `#C9A227`) en teinte/overlay ; les PNG fournissent relief 9-slice et formes. Pas d’intégration code avant validation look sur une capture.
