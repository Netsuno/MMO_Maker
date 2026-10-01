# maploadpublishedbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Charge la révision **publiée** d’une carte (jamais le brouillon seul).

*Source : `PostgresMapRepository.LoadPublishedByIdAsync`*

**Signature :** `maploadpublishedbyidasync(mapId)`

**Entrées :**
- `mapId` (`Guid`) — carte publiée

**Sorties :**
- (`StoredMap?`) — null si pas de publication

**Exemple :**
```csharp
StoredMap? published = await maps.LoadPublishedByIdAsync(mapId, ct);
// Charge la révision publiée d’une carte (jamais le brouillon seul)
// (StoredMap?) — null si pas de publication
```
