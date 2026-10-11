# composedtilesetdeleteasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Supprime le tileset composé, ses snapshots et son historique.

*Source : `IComposedTilesetRepository.DeleteAsync`*

**Signature :** `composedtilesetdeleteasync(tilesetId, cancellationToken?) → DeleteComposedTilesetResult`

**Entrées :**
- `tilesetId` (`Guid`) — tileset

**Sorties :**
- `result` (`DeleteComposedTilesetResult`) — Success, NotFound ou PersistenceFailed

**Refus :**
- id inconnu → NotFound
- les pixels de content.tiles restent

**Exemple :**
```csharp
var result = await repo.DeleteAsync(id, ct);
// Supprime le tileset composé, ses snapshots et son historique.
// composedtilesetdeleteasync(tilesetId, cancellationToken?) → DeleteComposedTilesetResult
```
