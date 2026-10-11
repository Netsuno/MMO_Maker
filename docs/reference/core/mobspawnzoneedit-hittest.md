# mobspawnzoneedit-hittest

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Trouve la zone sous une case. Une zone préférée gagne.

*Source : `MobSpawnZoneEdit.HitTest`*

**Signature :** `hittest(zones, tileX, tileY, preferredId?) → MobSpawnZone?`

**Entrées :**
- `zones` (`IReadOnlyList<MobSpawnZone>`) — ordre du document
- `tileX` (`int`) — colonne
- `tileY` (`int`) — ligne
- `preferredId` (`Guid?`) — sélection courante

**Sorties :**
- `zone` (`MobSpawnZone?`) — null si aucune

**Exemple :**
```csharp
MobSpawnZone? hit = MobSpawnZoneEdit.HitTest(zones, tileX, tileY, preferredId);
// Trouve la zone sous une case. Une zone préférée gagne.
// hittest(zones, tileX, tileY, preferredId?) → MobSpawnZone?
```
