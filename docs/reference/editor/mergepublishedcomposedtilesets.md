# mergepublishedcomposedtilesets

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Fusionne les tilesets composés publiés dans la palette TileAsset.

*Source : `TileAssetCatalogue.MergePublishedComposedTilesets` → `ComposedTilesetPlacement.Merge`*

**Signature :** `mergepublishedcomposedtilesets(published) → void`

**Entrées :**
- `published` (`IReadOnlyList<ComposedTilesetDefinition>?`) — seul le publié, pas le brouillon

**Sorties :**
- `effet` (`void`) — palettes locales conservées

**Exemple :**
```csharp
catalogue.MergePublishedComposedTilesets(published);
// Fusionne les tilesets composés publiés dans la palette TileAsset.
// mergepublishedcomposedtilesets(published) → void
```
