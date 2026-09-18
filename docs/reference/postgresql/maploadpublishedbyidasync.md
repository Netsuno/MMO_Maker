# maploadpublishedbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Charge la révision **publiée** d’une carte (jamais le brouillon seul).

*Source : `PostgresMapRepository.LoadPublishedByIdAsync`*

**Signature :** `maploadpublishedbyidasync(mapId)`

**Entrées :**
- `mapId` (`Guid`)

**Sorties :**
- (`StoredMap?`) — null si pas de publication
