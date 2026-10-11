# gamedatarepositoryset-mapobject

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Expose les dépôts objets de carte et tuiles composées.

*Source : `GameDataRepositorySet.MapObject`, `ComposedTileset` — deux arguments de plus au constructeur*

**Signature :** `mapobject → EditorMapObjectRepositoryBundle`

**Membres :**
- `MapObject` (`EditorMapObjectRepositoryBundle`) — dépôt objets de carte
- `ComposedTileset` (`EditorComposedTilesetRepositoryBundle`) — dépôt tuiles composées

**Entrées :**
- —

**Sorties :**
- `mapObject` (`EditorMapObjectRepositoryBundle`) — dépôt #146
- `composedTileset` (`EditorComposedTilesetRepositoryBundle`) — dépôt #148

**Exemple :**
```csharp
var objects = set.MapObject;
var tiles = set.ComposedTileset;
// Expose les dépôts objets de carte et tuiles composées.
// mapobject → EditorMapObjectRepositoryBundle
```
