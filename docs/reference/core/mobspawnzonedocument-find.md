# mobspawnzonedocument-find

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Cherche une zone par identifiant.

*Source : `MobSpawnZoneDocument.Find`*

**Signature :** `find(id) → MobSpawnZone?`

**Entrées :**
- `id` (`Guid`) — zone

**Sorties :**
- `zone` (`MobSpawnZone?`) — null si absente

**Exemple :**
```csharp
MobSpawnZone? zone = document.Find(id);
// Cherche une zone par identifiant.
// find(id) → MobSpawnZone?
```
