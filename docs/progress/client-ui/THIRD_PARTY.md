# Player world sprite — credit

**Chantier :** Netsun.

`Frog.Client/Assets/World/player.png` is **one 32×32 cell** from Eldiran’s
[32×32 RPG Character Sprites](https://opengameart.org/content/32x32-rpg-character-sprites)
(CC0), not Kenney 16×16, and not a Graal Online (or other commercial MMO) asset.
Magenta `#FF00FF` is chroma-keyed to alpha. The sheet yellow `#FFD800` is remapped
to a dark 1px outline so the world sprite has no gold chrome.

| Champ | Valeur |
| --- | --- |
| **File** | `Frog.Client/Assets/World/player.png` (south idle) plus `player-body.png` + `player-head.png`; walk MVP sheets `player-walk.png` / `player-walk-body.png` / `player-walk-head.png` (96×128 = 3×4 cells) |
| **Size** | 32×32 RGBA cells, nearest-neighbor ×1 in `MapViewRenderer` (world `tileSize` stays 32) |
| **Author** | Eldiran |
| **License** | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| **Source** | OpenGameArt sheet (no itch.io). Vendored at `tools/third_party/eldiran/RPGCharacterSprites32x32.png` |
| **Frame** | Idle: column 1, row 4 (0-based) — blue knight, south stand. Walk (same row): cols 0–2 down, 4–6 up, 8–10 right; left = flip of right |
| **Layers** | Body = rows 14–31; Head = rows 0–13. Reserved empty slots: tunic, armor, weapon. |

Re-extract the PNG:

```bash
python3 tools/generate-player-sprite.py
```

The client also embeds the PNG and keeps a matching fallback raster so the
yellow player ellipse cannot return if the loose file is missing.
