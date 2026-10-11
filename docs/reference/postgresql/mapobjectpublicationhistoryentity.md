# mapobjectpublicationhistoryentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Historique de publication d’un objet de carte.

*Source : `MapObjectPublicationHistoryEntity` — DbSet `MapObjectPublicationHistory`*

**Signature :** `mapobjectpublicationhistoryentity → MapObjectPublicationHistoryEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `MapObjectId` (`Guid`) — fk
- `SnapshotId` (`Guid`) — snapshot
- `Revision` (`long`) — révision
- `PublishedAtUtc` (`DateTimeOffset`) — date
- `MapObject` (`MapObjectEntity`) — navigation

**Entrées :**
- —

**Sorties :**
- `row` (`MapObjectPublicationHistoryEntity`) — content.map_object_publication_history

**Exemple :**
```csharp
Guid snapshotId = history.SnapshotId;
// Historique de publication d’un objet de carte.
// mapobjectpublicationhistoryentity → MapObjectPublicationHistoryEntity
```
