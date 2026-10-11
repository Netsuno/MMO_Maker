# mobspawnzoneedit-trymove

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Déplace le rectangle sans changer sa taille.

*Source : `MobSpawnZoneEdit.TryMove`*

**Signature :** `trymove(zone, mapWidth, mapHeight, tileX, tileY) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — cible
- `mapWidth` (`int`) — largeur
- `mapHeight` (`int`) — hauteur
- `tileX` (`int`) — nouveau coin
- `tileY` (`int`) — nouveau coin

**Sorties :**
- `ok` (`bool`) — déplacement accepté

**Refus :**
- le rectangle sortirait de la carte

**Exemple :**
```csharp
bool moved = MobSpawnZoneEdit.TryMove(zone, map.Width, map.Height, tileX, tileY);
// Déplace le rectangle sans changer sa taille.
// trymove(zone, mapWidth, mapHeight, tileX, tileY) → bool
```
