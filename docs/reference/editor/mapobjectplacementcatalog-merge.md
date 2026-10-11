# mapobjectplacementcatalog-merge

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ajoute les objets publiés au catalogue prefab. Le même id remplace.

*Source : `MapObjectPlacementCatalog.Merge`*

**Signature :** `merge(baseCatalog, published) → PrefabCatalog`

**Entrées :**
- `baseCatalog` (`PrefabCatalog`) — props intégrés
- `published` (`IEnumerable<MapObjectDefinition>?`) — objets publiés

**Sorties :**
- `catalog` (`PrefabCatalog`) — fusion

**Refus :**
- objet qui ne passe pas Validate ignoré

**Exemple :**
```csharp
PrefabCatalog catalog = MapObjectPlacementCatalog.Merge(baseCatalog, published);
// Ajoute les objets publiés au catalogue prefab. Le même id remplace.
// merge(baseCatalog, published) → PrefabCatalog
```
