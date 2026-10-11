# mobspawnzoneedit-trynormalizerect

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ramène un rectangle de tracé dans les bords de la carte.

*Source : `MobSpawnZoneEdit.TryNormalizeRect`*

**Signature :** `trynormalizerect(x0, y0, x1, y1, mapWidth, mapHeight, out tileX, out tileY, out width, out height) → bool`

**Entrées :**
- `x0` (`int`) — premier coin
- `y0` (`int`) — premier coin
- `x1` (`int`) — second coin
- `y1` (`int`) — second coin
- `mapWidth` (`int`) — largeur carte
- `mapHeight` (`int`) — hauteur carte

**Sorties :**
- `ok` (`bool`) — faux si la carte est vide
- `tileX` (`out int`) — coin normalisé
- `width` (`out int`) — au moins 1

**Refus :**
- carte de taille < 1

**Exemple :**
```csharp
bool ok = MobSpawnZoneEdit.TryNormalizeRect(x0, y0, x1, y1, map.Width, map.Height, out int tx, out int ty, out int w, out int h);
// Ramène un rectangle de tracé dans les bords de la carte.
// trynormalizerect(x0, y0, x1, y1, mapWidth, mapHeight, out tileX, out tileY, out width, out height) → bool
```
