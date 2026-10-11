# applyasync

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Pose les ordres du passage et compte ceux confirmés.

*Source : `MobSpawnZoneMaintenance.ApplyAsync`*

**Signature :** `applyasync(keeper, zones, aliveInstanceIds, utcNow, spawn, cancellationToken?) → int`

**Entrées :**
- `keeper` (`MobSpawnZoneKeeper`) — état des places
- `zones` (`IReadOnlyList<PublishedMobSpawnZone>`) — zones publiées
- `aliveInstanceIds` (`IReadOnlySet<Guid>`) — vivants
- `utcNow` (`DateTime`) — horloge
- `spawn` (`Func<MobSpawnOrder, CancellationToken, Task<Guid?>>`) — crée le monstre

**Sorties :**
- `spawned` (`int`) — apparitions confirmées

**Refus :**
- case hors zone → Abort
- spawn null ou Guid.Empty → Abort

**Exemple :**
```csharp
int n = await MobSpawnZoneMaintenance.ApplyAsync(keeper, zones, alive, utcNow, spawn, ct);
// Pose les ordres du passage et compte ceux confirmés.
// applyasync(keeper, zones, aliveInstanceIds, utcNow, spawn, cancellationToken?) → int
```
