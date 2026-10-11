# mapobjectcatalogentry

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Résumé d’objet de carte pour la liste.

*Source : `MapObjectCatalogEntry`*

**Signature :** `mapobjectcatalogentry → MapObjectCatalogEntry`

**Membres :**
- `MapObjectId` (`Guid`) — id
- `Name` (`string`) — nom
- `LogicalPath` (`string`) — chemin
- `PlacementId` (`string`) — pose
- `Revision` (`long`) — révision
- `Status` (`ContentPublishStatus`) — statut
- `PublishedRevision` (`long?`) — publication

**Entrées :**
- —

**Sorties :**
- `entry` (`MapObjectCatalogEntry`) — sans PNG

**Exemple :**
```csharp
string placement = entry.PlacementId;
// Résumé d’objet de carte pour la liste.
// mapobjectcatalogentry → MapObjectCatalogEntry
```
