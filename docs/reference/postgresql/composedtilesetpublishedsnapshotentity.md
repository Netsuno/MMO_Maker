# composedtilesetpublishedsnapshotentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Snapshot publié d’un tileset composé (ordre des membres, pas les pixels).

*Source : `ComposedTilesetPublishedSnapshotEntity` — DbSet `ComposedTilesetPublishedSnapshots`*

**Signature :** `composedtilesetpublishedsnapshotentity → ComposedTilesetPublishedSnapshotEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `ComposedTilesetId` (`Guid`) — fk cascade
- `Revision` (`long`) — figée
- `PublishedAtUtc` (`DateTimeOffset`) — date
- `Name` (`string`) — copie
- `LogicalPath` (`string`) — copie
- `MembersJson` (`string`) — ordre des tuiles
- `ComposedTileset` (`ComposedTilesetEntity`) — navigation

**Entrées :**
- —

**Sorties :**
- `row` (`ComposedTilesetPublishedSnapshotEntity`) — content.composed_tileset_published_snapshots

**Exemple :**
```csharp
string members = snapshot.MembersJson;
// Snapshot publié d’un tileset composé (ordre des membres, pas les pixels).
// composedtilesetpublishedsnapshotentity → ComposedTilesetPublishedSnapshotEntity
```
