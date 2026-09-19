# mapsaveasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Sauve un brouillon ou publie une carte (révision optimiste).

*Source : `PostgresMapRepository.SaveAsync` / `IMapRepository.SaveAsync`*

**Signature :** `mapsaveasync(request)`

**Entrées :**
- `request` (`SaveMapRequest`) — MapId?, Map, ExpectedRevision, Intent (SaveDraft | Publish)

**Sorties :**
- (`SaveMapResult`) — Success / Conflict / ValidationFailed / PersistenceFailed / NotDurable
