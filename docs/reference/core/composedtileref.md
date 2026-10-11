# composedtileref

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Une tuile choisie : identifiant de contenu et pixels 48×48.

*Source : `ComposedTileRef`*

**Signature :** `composedtileref → ComposedTileRef`

**Membres :**
- `TileAssetId` (`string`) — empreinte des pixels
- `DisplayName` (`string?`) — nom d’affichage, optionnel
- `NormalizedRgba` (`byte[]`) — 9216 octets RGBA prémultipliés

**Entrées :**
- —

**Sorties :**
- `tile` (`ComposedTileRef`) — membre du tileset

**Exemple :**
```csharp
var tile = new ComposedTileRef { TileAssetId = id.ToString(), NormalizedRgba = rgba };
// Une tuile choisie : identifiant de contenu et pixels 48×48.
// composedtileref → ComposedTileRef
```
