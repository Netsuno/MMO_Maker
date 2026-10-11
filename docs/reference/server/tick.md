# tick

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Calcule les apparitions dues et arme le délai à la mort observée.

*Source : `MobSpawnZoneKeeper.Tick` — constructeur `MobSpawnZoneKeeper(pickTile)`*

**Signature :** `tick(zones, aliveInstanceIds, utcNow) → IReadOnlyList<MobSpawnOrder>`

**Entrées :**
- `zones` (`IReadOnlyList<PublishedMobSpawnZone>`) — zones publiées
- `aliveInstanceIds` (`IReadOnlySet<Guid>`) — monstres encore vivants
- `utcNow` (`DateTime`) — horloge du passage

**Sorties :**
- `orders` (`IReadOnlyList<MobSpawnOrder>`) — places à remplir maintenant

**Refus :**
- tuile hors rectangle jamais ordonnée
- délai non échu : aucune réapparition

**Exemple :**
```csharp
var keeper = new MobSpawnZoneKeeper(zone => MobSpawnZoneEdit.PickTile(zone, Random.Shared));
IReadOnlyList<MobSpawnOrder> orders = keeper.Tick(zones, alive, DateTime.UtcNow);
// Calcule les apparitions dues et arme le délai à la mort observée.
// tick(zones, aliveInstanceIds, utcNow) → IReadOnlyList<MobSpawnOrder>
```
