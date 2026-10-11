# savemapobjectrequest

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Requête d’enregistrement d’un objet de carte.

*Source : `SaveMapObjectRequest` — résultats `SaveMapObjectResult` : Success, Conflict, ValidationFailed, PersistenceFailed, NotDurable*

**Signature :** `savemapobjectrequest → SaveMapObjectRequest`

**Membres :**
- `MapObjectId` (`Guid?`) — null = création
- `Definition` (`MapObjectDefinition`) — requis
- `ExpectedRevision` (`long`) — requis
- `Intent` (`SaveContentIntent`) — SaveDraft par défaut

**Entrées :**
- `definition` (`MapObjectDefinition`) — contenu
- `expectedRevision` (`long`) — 0 à la création

**Sorties :**
- `request` (`SaveMapObjectRequest`) — passée à SaveAsync

**Exemple :**
```csharp
var request = new SaveMapObjectRequest {
    Definition = definition, ExpectedRevision = 0, Intent = SaveContentIntent.SaveDraft };
// Requête d’enregistrement d’un objet de carte.
// savemapobjectrequest → SaveMapObjectRequest
```
