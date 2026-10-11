# storedmapobject

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Objet de carte relu avec sa révision.

*Source : `StoredMapObject`*

**Signature :** `storedmapobject → StoredMapObject`

**Membres :**
- `MapObjectId` (`Guid`) — id
- `Definition` (`MapObjectDefinition`) — contenu
- `Revision` (`long`) — révision
- `Status` (`ContentPublishStatus`) — statut
- `PublishedRevision` (`long?`) — publication

**Entrées :**
- —

**Sorties :**
- `stored` (`StoredMapObject`) — ligne + définition

**Exemple :**
```csharp
Guid id = stored.MapObjectId;
// Objet de carte relu avec sa révision.
// storedmapobject → StoredMapObject
```
