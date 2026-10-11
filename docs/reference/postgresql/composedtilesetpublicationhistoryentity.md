# composedtilesetpublicationhistoryentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Historique de publication d’un tileset composé.

*Source : `ComposedTilesetPublicationHistoryEntity` — DbSet `ComposedTilesetPublicationHistory`*

**Signature :** `composedtilesetpublicationhistoryentity → ComposedTilesetPublicationHistoryEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `ComposedTilesetId` (`Guid`) — fk
- `SnapshotId` (`Guid`) — snapshot
- `Revision` (`long`) — révision
- `PublishedAtUtc` (`DateTimeOffset`) — date
- `ComposedTileset` (`ComposedTilesetEntity`) — navigation

**Entrées :**
- —

**Sorties :**
- `row` (`ComposedTilesetPublicationHistoryEntity`) — content.composed_tileset_publication_history

**Exemple :**
```csharp
Guid snapshotId = history.SnapshotId;
// Historique de publication d’un tileset composé.
// composedtilesetpublicationhistoryentity → ComposedTilesetPublicationHistoryEntity
```
