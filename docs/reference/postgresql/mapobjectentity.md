# mapobjectentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ligne content.map_objects (brouillon).

*Source : `MapObjectEntity` — DbSet `FrogDbContext.MapObjects`*

**Signature :** `mapobjectentity → MapObjectEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `Name` (`string`) — varchar 120
- `LogicalPath` (`string`) — unique
- `PlacementId` (`string`) — unique
- `FootprintWidthTiles` (`int`) — cases
- `FootprintHeightTiles` (`int`) — cases
- `Width` (`int`) — pixels
- `Height` (`int`) — pixels
- `Sha256Hex` (`string`) — 64 hex
- `Status` (`ContentPublishStatus`) — brouillon ou publié
- `Revision` (`long`) — optimiste
- `PublishedRevision` (`long?`) — dernière publication
- `PublishedSnapshotId` (`Guid?`) — snapshot courant
- `CreatedAtUtc` (`DateTimeOffset`) — création
- `UpdatedAtUtc` (`DateTimeOffset`) — mise à jour

**Entrées :**
- —

**Sorties :**
- `row` (`MapObjectEntity`) — table content.map_objects

**Exemple :**
```csharp
db.MapObjects.Add(row);
// Ligne content.map_objects (brouillon).
// mapobjectentity → MapObjectEntity
```
