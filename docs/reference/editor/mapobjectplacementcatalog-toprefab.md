# mapobjectplacementcatalog-toprefab

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Projette un objet de carte en prefab face sud.

*Source : `MapObjectPlacementCatalog.ToPrefab`*

**Signature :** `toprefab(source) → PrefabDefinition`

**Entrées :**
- `source` (`MapObjectDefinition`) — objet valide

**Sorties :**
- `prefab` (`PrefabDefinition`) — id = PlacementId

**Exemple :**
```csharp
PrefabDefinition prefab = MapObjectPlacementCatalog.ToPrefab(source);
// Projette un objet de carte en prefab face sud.
// toprefab(source) → PrefabDefinition
```
