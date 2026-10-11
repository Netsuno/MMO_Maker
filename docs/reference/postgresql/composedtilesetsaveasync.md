# composedtilesetsaveasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Enregistre ou publie un tileset composé. Les pixels vont dans content.tiles.

*Source : `IComposedTilesetRepository.SaveAsync` — `PostgresComposedTilesetRepository`, `InMemoryComposedTilesetRepository`*

**Signature :** `composedtilesetsaveasync(request, cancellationToken?) → SaveComposedTilesetResult`

**Entrées :**
- `request` (`SaveComposedTilesetRequest`) — TilesetId?, Definition, ExpectedRevision, Intent

**Sorties :**
- `result` (`SaveComposedTilesetResult`) — Success, Conflict, ValidationFailed, PersistenceFailed ou NotDurable

**Refus :**
- aucune tuile ou pixels autres que 48×48
- chemin logique déjà pris
- révision dépassée
- `AllowsSave` faux (`InMemoryDemo`) → NotDurable. `InMemoryTest` enregistre en mémoire

**Exemple :**
```csharp
var result = await repo.SaveAsync(request, ct);
// Enregistre ou publie un tileset composé. Les pixels vont dans content.tiles.
// composedtilesetsaveasync(request, cancellationToken?) → SaveComposedTilesetResult
```
