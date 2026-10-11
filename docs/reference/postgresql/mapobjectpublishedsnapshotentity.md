# mapobjectpublishedsnapshotentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Snapshot publié d’un objet de carte, PNG inclus.

*Source : `MapObjectPublishedSnapshotEntity` — DbSet `MapObjectPublishedSnapshots`*

**Signature :** `mapobjectpublishedsnapshotentity → MapObjectPublishedSnapshotEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `MapObjectId` (`Guid`) — fk cascade
- `Revision` (`long`) — révision figée
- `PublishedAtUtc` (`DateTimeOffset`) — date
- `Name` (`string`) — copie
- `LogicalPath` (`string`) — copie
- `PlacementId` (`string`) — copie
- `FootprintWidthTiles` (`int`) — copie
- `FootprintHeightTiles` (`int`) — copie
- `Width` (`int`) — copie
- `Height` (`int`) — copie
- `Sha256Hex` (`string`) — copie
- `PngBytes` (`byte[]?`) — PNG du snapshot
- `MapObject` (`MapObjectEntity`) — navigation

**Entrées :**
- —

**Sorties :**
- `row` (`MapObjectPublishedSnapshotEntity`) — content.map_object_published_snapshots

**Exemple :**
```csharp
byte[]? png = snapshot.PngBytes;
// Snapshot publié d’un objet de carte, PNG inclus.
// mapobjectpublishedsnapshotentity → MapObjectPublishedSnapshotEntity
```
