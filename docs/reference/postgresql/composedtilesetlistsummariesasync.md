# composedtilesetlistsummariesasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Liste les résumés, avec le nombre de tuiles.

*Source : `IComposedTilesetRepository.ListSummariesAsync`*

**Signature :** `composedtilesetlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<ComposedTilesetCatalogEntry>`

**Entrées :**
- `search` (`string?`) — filtre nom
- `statusFilter` (`ContentPublishStatus?`) — statut

**Sorties :**
- `rows` (`IReadOnlyList<ComposedTilesetCatalogEntry>`) — TileCount inclus

**Exemple :**
```csharp
var rows = await repo.ListSummariesAsync(null, null, ct);
// Liste les résumés, avec le nombre de tuiles.
// composedtilesetlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<ComposedTilesetCatalogEntry>
```
