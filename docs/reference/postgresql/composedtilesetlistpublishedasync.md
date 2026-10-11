# composedtilesetlistpublishedasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Liste les tilesets dont un snapshot est publié. Un brouillon n’y est pas.

*Source : `IPublishedComposedTilesetCatalog.ListPublishedAsync` — Postgres, InMemory, EmptyPublishedComposedTilesetCatalog*

**Signature :** `composedtilesetlistpublishedasync(cancellationToken?) → IReadOnlyList<ComposedTilesetDefinition>`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `published` (`IReadOnlyList<ComposedTilesetDefinition>`) — ordre par nom côté Postgres

**Exemple :**
```csharp
var published = await catalog.ListPublishedAsync(ct);
// Liste les tilesets dont un snapshot est publié. Un brouillon n’y est pas.
// composedtilesetlistpublishedasync(cancellationToken?) → IReadOnlyList<ComposedTilesetDefinition>
```
