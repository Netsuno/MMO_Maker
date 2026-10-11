# listmobspawnzonesasync

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Lit les zones de monstres du snapshot publié, pas du brouillon.

*Source : `IPublishedWorldCatalog.ListMobSpawnZonesAsync` — `PostgresPublishedWorldCatalog`, `NullPublishedWorldCatalog`*

**Signature :** `listmobspawnzonesasync(cancellationToken?) → IReadOnlyList<PublishedMobSpawnZone>`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation SQL

**Sorties :**
- `zones` (`IReadOnlyList<PublishedMobSpawnZone>`) — rectangles publiés, carte runtime

**Refus :**
- carte sans snapshot publié ignorée
- JSON vide ou illisible ignoré

**Exemple :**
```csharp
IReadOnlyList<PublishedMobSpawnZone> zones = await world.ListMobSpawnZonesAsync(ct);
// NullPublishedWorldCatalog renvoie une liste vide
// Lit les zones de monstres du snapshot publié, pas du brouillon.
// listmobspawnzonesasync(cancellationToken?) → IReadOnlyList<PublishedMobSpawnZone>
```
