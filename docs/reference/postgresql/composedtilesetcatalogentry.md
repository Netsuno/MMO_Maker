# composedtilesetcatalogentry

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Résumé de tileset composé, avec le nombre de tuiles.

*Source : `ComposedTilesetCatalogEntry`*

**Signature :** `composedtilesetcatalogentry → ComposedTilesetCatalogEntry`

**Membres :**
- `TilesetId` (`Guid`) — id
- `Name` (`string`) — nom
- `LogicalPath` (`string`) — chemin
- `Revision` (`long`) — révision
- `Status` (`ContentPublishStatus`) — statut
- `PublishedRevision` (`long?`) — publication
- `TileCount` (`int`) — membres

**Entrées :**
- —

**Sorties :**
- `entry` (`ComposedTilesetCatalogEntry`) — ligne de liste

**Exemple :**
```csharp
int n = entry.TileCount;
// Résumé de tileset composé, avec le nombre de tuiles.
// composedtilesetcatalogentry → ComposedTilesetCatalogEntry
```
