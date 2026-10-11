# mapobjectloadbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Charge le brouillon courant d’un objet de carte.

*Source : `IMapObjectRepository.LoadByIdAsync` — Postgres et InMemory*

**Signature :** `mapobjectloadbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?`

**Entrées :**
- `mapObjectId` (`Guid`) — objet

**Sorties :**
- `stored` (`StoredMapObject?`) — null si absent

**Exemple :**
```csharp
StoredMapObject? stored = await repo.LoadByIdAsync(id, ct);
// Charge le brouillon courant d’un objet de carte.
// mapobjectloadbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?
```
