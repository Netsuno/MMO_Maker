# mobspawnzoneedit-tryremoveentry

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Retire une ligne de la zone.

*Source : `MobSpawnZoneEdit.TryRemoveEntry`*

**Signature :** `tryremoveentry(zone, entryId) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — cible
- `entryId` (`Guid`) — ligne

**Sorties :**
- `removed` (`bool`) — faux si absente

**Exemple :**
```csharp
bool removed = MobSpawnZoneEdit.TryRemoveEntry(zone, entryId);
// Retire une ligne de la zone.
// tryremoveentry(zone, entryId) → bool
```
