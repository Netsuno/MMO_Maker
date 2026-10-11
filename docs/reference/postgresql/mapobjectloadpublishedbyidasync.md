# mapobjectloadpublishedbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Charge le snapshot publié d’un objet de carte.

*Source : `IMapObjectRepository.LoadPublishedByIdAsync` — Postgres et InMemory*

**Signature :** `mapobjectloadpublishedbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?`

**Entrées :**
- `mapObjectId` (`Guid`) — objet

**Sorties :**
- `stored` (`StoredMapObject?`) — null sans publication

**Exemple :**
```csharp
StoredMapObject? published = await repo.LoadPublishedByIdAsync(id, ct);
// Charge le snapshot publié d’un objet de carte.
// mapobjectloadpublishedbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?
```
