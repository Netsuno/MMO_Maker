# mapobjectdeleteasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Supprime l’objet, son historique et ses snapshots.

*Source : `IMapObjectRepository.DeleteAsync`*

**Signature :** `mapobjectdeleteasync(mapObjectId, cancellationToken?) → DeleteMapObjectResult`

**Entrées :**
- `mapObjectId` (`Guid`) — objet

**Sorties :**
- `result` (`DeleteMapObjectResult`) — Success, NotFound ou PersistenceFailed

**Refus :**
- id inconnu → NotFound

**Exemple :**
```csharp
var result = await repo.DeleteAsync(id, ct);
// Supprime l’objet, son historique et ses snapshots.
// mapobjectdeleteasync(mapObjectId, cancellationToken?) → DeleteMapObjectResult
```
