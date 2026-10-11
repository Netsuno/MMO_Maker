# mobspawnzoneedit-contains

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Dit si la case est dans le rectangle.

*Source : `MobSpawnZoneEdit.Contains`*

**Signature :** `contains(zone, tileX, tileY) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — rectangle
- `tileX` (`int`) — colonne
- `tileY` (`int`) — ligne

**Sorties :**
- `inside` (`bool`) — vrai si la case est dans la zone

**Exemple :**
```csharp
bool inside = MobSpawnZoneEdit.Contains(zone, tileX, tileY);
// Dit si la case est dans le rectangle.
// contains(zone, tileX, tileY) → bool
```
