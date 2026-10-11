# mobspawnzoneedit-trycopy

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Copie une zone vers un autre coin.

*Source : `MobSpawnZoneEdit.TryCopy`*

**Signature :** `trycopy(document, mapWidth, mapHeight, sourceId, tileX, tileY, out copy, out error) → bool`

**Entrées :**
- `document` (`MobSpawnZoneDocument`) — porteur
- `sourceId` (`Guid`) — zone source
- `tileX` (`int`) — coin de la copie
- `tileY` (`int`) — coin de la copie

**Sorties :**
- `ok` (`bool`) — copie ajoutée
- `copy` (`out MobSpawnZone`) — nouvelle zone
- `error` (`out string?`) — motif

**Refus :**
- source absente
- plafond de zones
- hors carte

**Exemple :**
```csharp
MobSpawnZoneEdit.TryCopy(document, map.Width, map.Height, sourceId, tileX, tileY, out var copy, out var error);
// Copie une zone vers un autre coin.
// trycopy(document, mapWidth, mapHeight, sourceId, tileX, tileY, out copy, out error) → bool
```
