# savecomposedtilesetrequest

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Requête d’enregistrement d’un tileset composé.

*Source : `SaveComposedTilesetRequest` — `SaveComposedTilesetResult` : Success, Conflict, ValidationFailed, PersistenceFailed, NotDurable*

**Signature :** `savecomposedtilesetrequest → SaveComposedTilesetRequest`

**Membres :**
- `TilesetId` (`Guid?`) — null = création
- `Definition` (`ComposedTilesetDefinition`) — requis
- `ExpectedRevision` (`long`) — requis
- `Intent` (`SaveContentIntent`) — SaveDraft par défaut

**Entrées :**
- `definition` (`ComposedTilesetDefinition`) — contenu
- `expectedRevision` (`long`) — 0 à la création

**Sorties :**
- `request` (`SaveComposedTilesetRequest`) — passée à SaveAsync

**Exemple :**
```csharp
var request = new SaveComposedTilesetRequest {
    Definition = definition, ExpectedRevision = 0, Intent = SaveContentIntent.Publish };
// Requête d’enregistrement d’un tileset composé.
// savecomposedtilesetrequest → SaveComposedTilesetRequest
```
