# maploadbyidasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Charge une carte stockée (brouillon ou état courant selon le repo).

*Source : `PostgresMapRepository.LoadByIdAsync`*

**Signature :** `maploadbyidasync(mapId)`

**Entrées :**
- `mapId` (`Guid`) — carte stockée

**Sorties :**
- (`StoredMap?`) — null si absente

**Exemple :**
```csharp
StoredMap? map = await maps.LoadByIdAsync(mapId, ct);
// Charge une carte stockée (brouillon ou état courant selon le repo)
// (StoredMap?) — null si absente
```
