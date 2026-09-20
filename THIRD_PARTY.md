# Third-party assets

This repository redistributes a **minimal, renamed subset** of third-party game UI
art for the Frog WinForms client. No secrets. No public-release claim.

Canonical pack tree: `Frog.Client/Assets/Ui/` (DA kit: `KIT-SELECTION.md` + this file’s copy).

## Kenney — UI Pack (RPG Expansion)

- **Source:** https://kenney.nl/assets/ui-pack-rpg-expansion
- **Author:** Kenney Vleugels (www.kenney.nl)
- **License:** [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
- **Renamed files (see `KIT-SELECTION.md`):**
  - `frames/panel.png` ← `panel_brown.png`
  - `frames/panel_inset.png` ← `panelInset_brown.png`
  - `slots/slot.png` ← `buttonSquare_brown.png` (+ `slot_pressed.png`)
  - `menu/btn_round.png` ← `buttonRound_brown.png`
  - `bars/hp_*` ← `barRed_horizontal*`
  - `bars/mp_*` ← `barBlue_horizontal*` (`mp_mid.png` ← `barBlue_horizontalBlue.png`)
  - `bars/track_*` ← `barBack_horizontal*`
  - `bars/xp_*` ← `barYellow_horizontal*` (shipped; not wired — no XP max on the wire)
  - `chrome/btn_long.png` ← `buttonLong_brown.png`
  - `chrome/arrow_*.png` ← `arrowBrown_*`

Kenney’s pack license (CC0) allows use in personal and commercial projects.
Credit is not mandatory; FRoG still records the origin here.

## game-icons.net

- **Source:** https://game-icons.net/
- **License:** [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/)

See [CREDITS.md](CREDITS.md) and `Frog.Client/Assets/Ui/THIRD_PARTY.md`.

## Original FRoG world sprite (not a third-party pack)

`Frog.Client/Assets/World/player.png` is an original CC0 16×16 top-down humanoid
authored for FRoG in this repository. See
[docs/progress/client-ui/THIRD_PARTY.md](docs/progress/client-ui/THIRD_PARTY.md).
