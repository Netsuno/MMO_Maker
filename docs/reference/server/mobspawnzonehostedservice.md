# mobspawnzonehostedservice

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Service d’arrière-plan qui réapparaît les monstres des zones publiées.

*Source : `MobSpawnZoneHostedService` — `Interval` public, `ExecuteAsync` protected*

**Signature :** `interval → TimeSpan`

**Membres :**
- `Interval` (`TimeSpan`) — période entre deux passages

**Entrées :**
- —

**Sorties :**
- `interval` (`TimeSpan`) — une seconde

**Exemple :**
```csharp
// MobSpawnZoneHostedService.Interval == TimeSpan.FromSeconds(1)
// ExecuteAsync est protected : un passage appelle ListMobSpawnZonesAsync puis ApplyAsync.
// Le centre de tuile utilise WorldMetrics.TileCenterToPixels (grille 32).
```
