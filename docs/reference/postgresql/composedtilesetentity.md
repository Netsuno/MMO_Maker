# composedtilesetentity

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ligne content.composed_tilesets. Les pixels sont dans content.tiles.

*Source : `ComposedTilesetEntity` — DbSet `ComposedTilesets`*

**Signature :** `composedtilesetentity → ComposedTilesetEntity`

**Membres :**
- `Id` (`Guid`) — pk
- `Name` (`string`) — varchar 120
- `LogicalPath` (`string`) — unique
- `MembersJson` (`string`) — tableau id, name
- `Status` (`ContentPublishStatus`) — statut
- `Revision` (`long`) — optimiste
- `PublishedRevision` (`long?`) — publication
- `PublishedSnapshotId` (`Guid?`) — snapshot
- `CreatedAtUtc` (`DateTimeOffset`) — création
- `UpdatedAtUtc` (`DateTimeOffset`) — mise à jour

**Entrées :**
- —

**Sorties :**
- `row` (`ComposedTilesetEntity`) — brouillon

**Exemple :**
```csharp
string members = row.MembersJson;
// Ligne content.composed_tilesets. Les pixels sont dans content.tiles.
// composedtilesetentity → ComposedTilesetEntity
```
