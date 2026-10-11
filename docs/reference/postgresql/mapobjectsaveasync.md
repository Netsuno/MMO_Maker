# mapobjectsaveasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Enregistre ou publie un objet de carte (révision optimiste).

*Source : `IMapObjectRepository.SaveAsync` — `PostgresMapObjectRepository`, `InMemoryMapObjectRepository`*

**Signature :** `mapobjectsaveasync(request, cancellationToken?) → SaveMapObjectResult`

**Entrées :**
- `request` (`SaveMapObjectRequest`) — MapObjectId?, Definition, ExpectedRevision, Intent

**Sorties :**
- `result` (`SaveMapObjectResult`) — Success, Conflict, ValidationFailed, PersistenceFailed ou NotDurable

**Refus :**
- Definition.Validate faux
- ExpectedRevision dépassée → Conflict
- `AllowsSave` faux (`InMemoryDemo`) → NotDurable. `InMemoryTest` enregistre en mémoire

**Exemple :**
```csharp
var result = await repo.SaveAsync(request, ct);
if (result is SaveMapObjectResult.Success ok) { }
// Enregistre ou publie un objet de carte (révision optimiste).
// mapobjectsaveasync(request, cancellationToken?) → SaveMapObjectResult
```
