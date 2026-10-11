# mapobjectlistpublishedasync

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Liste les définitions publiées pour la palette Objets.

*Source : `IPublishedMapObjectCatalog.ListPublishedAsync` — Postgres, InMemory, EmptyPublishedMapObjectCatalog*

**Signature :** `mapobjectlistpublishedasync(cancellationToken?) → IReadOnlyList<MapObjectDefinition>`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `published` (`IReadOnlyList<MapObjectDefinition>`) — snapshots seulement

**Exemple :**
```csharp
var published = await catalog.ListPublishedAsync(ct);
// EmptyPublishedMapObjectCatalog.Instance renvoie une liste vide
// Liste les définitions publiées pour la palette Objets.
// mapobjectlistpublishedasync(cancellationToken?) → IReadOnlyList<MapObjectDefinition>
```
