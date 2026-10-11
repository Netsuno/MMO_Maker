# composedtilesetloadbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Charge le brouillon courant d’un tileset composé.

*Source : `IComposedTilesetRepository.LoadByIdAsync`*

**Signature :** `composedtilesetloadbyidasync(tilesetId, cancellationToken?) → StoredComposedTileset?`

**Entrées :**
- `tilesetId` (`Guid`) — tileset

**Sorties :**
- `stored` (`StoredComposedTileset?`) — null si absent

**Exemple :**
```csharp
StoredComposedTileset? stored = await repo.LoadByIdAsync(id, ct);
// Charge le brouillon courant d’un tileset composé.
// composedtilesetloadbyidasync(tilesetId, cancellationToken?) → StoredComposedTileset?
```
