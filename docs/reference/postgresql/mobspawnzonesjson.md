# mobspawnzonesjson

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Colonne JSON des zones sur le brouillon et sur le snapshot de carte.

*Source : `MapEntity.MobSpawnZonesJson`, `MapPublishedSnapshotEntity.MobSpawnZonesJson`*

**Signature :** `mobspawnzonesjson → string?`

**Membres :**
- `MapEntity.MobSpawnZonesJson` (`string?`) — world.maps.mob_spawn_zones_json
- `MapPublishedSnapshotEntity.MobSpawnZonesJson` (`string?`) — snapshot publié

**Entrées :**
- —

**Sorties :**
- `json` (`string?`) — null = aucune zone

**Exemple :**
```csharp
string? draft = map.MobSpawnZonesJson;
string? published = snapshot.MobSpawnZonesJson;
// Colonne JSON des zones sur le brouillon et sur le snapshot de carte.
// mobspawnzonesjson → string?
```
