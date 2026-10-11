# composedtilesetplacement-merge

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Garde les palettes locales et ajoute les tilesets publiés.

*Source : `ComposedTilesetPlacement.Merge`*

**Signature :** `merge(local, published) → IReadOnlyList<WorkingTileset>`

**Entrées :**
- `local` (`IReadOnlyList<WorkingTileset>?`) — palettes sans id serveur
- `published` (`IEnumerable<ComposedTilesetDefinition>?`) — catalogue publié

**Sorties :**
- `merged` (`IReadOnlyList<WorkingTileset>`) — locales puis publiées

**Refus :**
- palette locale invalide ignorée
- tileset publié invalide ignoré
- une palette avec ServerTilesetId n’est pas recopiée comme locale

**Exemple :**
```csharp
IReadOnlyList<WorkingTileset> merged = ComposedTilesetPlacement.Merge(local, published);
// Garde les palettes locales et ajoute les tilesets publiés.
// merge(local, published) → IReadOnlyList<WorkingTileset>
```
