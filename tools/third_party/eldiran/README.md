# Eldiran — 32×32 RPG Character Sprites (CC0)

- **Author:** Eldiran
- **Source:** https://opengameart.org/content/32x32-rpg-character-sprites
- **License:** [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
- **File:** `RPGCharacterSprites32x32.png` (384×672, 12×21 cells of 32×32)

FRoG ships the south idle cell as `player.png` plus **body / head** layers
(`player-body.png`, `player-head.png`) and a 3×4 walk sheet
(`player-walk.png` / `player-walk-body.png` / `player-walk-head.png`) so tunic /
armor / weapon can overlay later. See `tools/generate-player-sprite.py`. Magenta
`#FF00FF` is chroma-keyed to alpha. The sheet’s yellow outline `#FFD800` is
remapped to a dark 1px outline (`#1A120E`) so the world sprite has no gold
chrome. No Graal sheets.

Idle cell: **column 1, row 4** (0-based) — blue knight, second south walk frame
(standing). Walk on the same row: **cols 0–2** down, **4–6** up, **8–10** right;
left-facing frames are a horizontal flip of right.

NPC walk (`npc-walk.png`) uses the same column layout on **row 9** (brown
villager). See `tools/generate-npc-monster-sprites.py`. Monster slime sheets
are original procedural art, not this file.
