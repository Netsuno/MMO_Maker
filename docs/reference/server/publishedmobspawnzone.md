# publishedmobspawnzone

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Zone publiée reliée à l’identifiant runtime des monstres.

*Source : `PublishedMobSpawnZone`*

**Signature :** `publishedmobspawnzone(mapId, runtimeMapId, zone) → PublishedMobSpawnZone`

**Membres :**
- `MapId` (`Guid`) — carte
- `RuntimeMapId` (`int`) — carte runtime
- `Zone` (`MobSpawnZone`) — définition

**Entrées :**
- `mapId` (`Guid`) — carte canonique
- `runtimeMapId` (`int`) — identifiant de session
- `zone` (`MobSpawnZone`) — rectangle et lignes

**Sorties :**
- `record` (`PublishedMobSpawnZone`) — immuable

**Exemple :**
```csharp
var published = new PublishedMobSpawnZone(mapId, runtimeMapId, zone);
// Zone publiée reliée à l’identifiant runtime des monstres.
// publishedmobspawnzone(mapId, runtimeMapId, zone) → PublishedMobSpawnZone
```
