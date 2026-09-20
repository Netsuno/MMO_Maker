# Player world sprite — credit

**Chantier :** Netsun.

`Frog.Client/Assets/World/player.png` is an **original** 16×16 top-down humanoid
drawn in-repo (`tools/generate-player-sprite.py`, raw PNG bytes). It is **not**
a third-party pack, not Kenney, and not a Graal Online (or other commercial MMO)
asset. The look is a generic chunky classic top-down 2D MMO silhouette
(cloak / hair / tunic) inspired only in the broad sense.

| Champ | Valeur |
| --- | --- |
| **File** | `Frog.Client/Assets/World/player.png` |
| **Size** | 16×16 RGBA, nearest-neighbor ×2 in `MapViewRenderer` |
| **Author** | Original for FRoG (this repository) |
| **License** | [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) |
| **Source** | Authored in code — no itch.io / Kenney / OpenGameArt download |

Re-author the PNG (do not fetch an external sheet):

```bash
python3 tools/generate-player-sprite.py
```

The client also embeds the PNG and keeps a matching fallback raster so the
yellow player ellipse cannot return if the loose file is missing.
