# storedcomposedtileset

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Tileset composé relu avec sa révision.

*Source : `StoredComposedTileset`*

**Signature :** `storedcomposedtileset → StoredComposedTileset`

**Membres :**
- `TilesetId` (`Guid`) — id
- `Definition` (`ComposedTilesetDefinition`) — contenu
- `Revision` (`long`) — révision
- `Status` (`ContentPublishStatus`) — statut
- `PublishedRevision` (`long?`) — publication

**Entrées :**
- —

**Sorties :**
- `stored` (`StoredComposedTileset`) — ligne + définition

**Exemple :**
```csharp
Guid id = stored.TilesetId;
// Tileset composé relu avec sa révision.
// storedcomposedtileset → StoredComposedTileset
```
