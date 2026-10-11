# mapsaveasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Sauve un brouillon ou publie une carte (révision optimiste).

*Source : `PostgresMapRepository.SaveAsync` / `IMapRepository.SaveAsync`*

**Signature :** `mapsaveasync(request)`

**Entrées :**
- `request` (`SaveMapRequest`) — MapId?, Map, ExpectedRevision, Intent (SaveDraft | Publish), `MobSpawnZones?`

**Sorties :**
- (`SaveMapResult`) — Success / Conflict / ValidationFailed / PersistenceFailed / NotDurable
- `MobSpawnZones` (`MobSpawnZoneDocument?`) — null laisse la colonne inchangée à la mise à jour ; document vide efface les zones. Écrit `mob_spawn_zones_json` (brouillon et snapshot si Publish). `TryValidate` refuse un rectangle hors carte

**Exemple :**
```csharp
var result = await maps.SaveAsync(request, ct); // Intent SaveDraft | Publish
if (result is not SaveMapResult.Success) return;
// Sauve un brouillon ou publie une carte (révision optimiste)
```
