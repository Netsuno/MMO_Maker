# mapobjectlistsummariesasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Liste les résumés, filtrés par texte et par statut.

*Source : `IMapObjectRepository.ListSummariesAsync`*

**Signature :** `mapobjectlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<MapObjectCatalogEntry>`

**Entrées :**
- `search` (`string?`) — filtre nom
- `statusFilter` (`ContentPublishStatus?`) — brouillon ou publié

**Sorties :**
- `rows` (`IReadOnlyList<MapObjectCatalogEntry>`) — résumés

**Exemple :**
```csharp
var rows = await repo.ListSummariesAsync(search: null, statusFilter: null, ct);
// Liste les résumés, filtrés par texte et par statut.
// mapobjectlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<MapObjectCatalogEntry>
```
