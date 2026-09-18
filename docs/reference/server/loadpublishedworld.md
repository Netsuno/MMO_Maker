# loadpublishedworld

← [Server](README.md) · [Référence](../README.md)

Remplace le monde runtime par les cartes publiées PostgreSQL.

*Source : `MapService.LoadPublishedWorld`*

**Signature :** `loadpublishedworld(maps, catalog)`

**Entrées :**
- `maps` (`IReadOnlyList<PublishedMapRuntimeEntry>`) — cartes publiées (non vide)
- `catalog` (`IPublishedWorldCatalog`) — catalogue monde / spawn

**Sorties :**
- état `MapService` rechargé (chunks, warps, empreintes)

**Refus :**
- `maps` vide → `InvalidOperationException`
