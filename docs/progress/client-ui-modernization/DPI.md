# DPI / lisibilité — E8

**Propriétaire :** Netsun

Le chrome WinForms utilise `AutoScaleMode.Font` + `AutoScaleDimensions` 96 DPI (100 %). Cible : **100 / 125 / 150 %** Windows.

## Règles

- Layout en DIP (pas de positions recalculées sur le timer 16 ms).
- HUD ancré (`LayoutGameHud` sur `Resize` du viewport seulement).
- Hitboxes boutons / slots : taille minimale ~28–36 px logiques.
- Cadres double filet or : 1 px + 1 px ; éviter le blur (pas de Scaling bitmap du chrome).
- Carte : nearest-neighbor inchangé (`MapViewRenderer`).
- Pas de chiffre FPS inventé — mesurer sur machine Windows ou se taire.

## Résolutions

| Surface | Note |
| --- | --- |
| 1366×768 | HUD compact ; tabs 360 px ; chat 360×200 |
| 1920×1080 | Même ancres ; tabs hauteur clamp 250–700 (smokes Phase 8) |
| 100 % | Référence design 1280×720 |
| 150 % | Contrôles System Font ; vérifier Options + chat input |

Linux CI : compile `EnableWindowsTargeting` seulement — pas de revue visuelle DPI ici.
