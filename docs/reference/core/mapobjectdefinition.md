# mapobjectdefinition

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Prop plaçable, distinct d’un objet d’inventaire.

*Source : `MapObjectDefinition`*

**Signature :** `mapobjectdefinition → MapObjectDefinition`

**Membres :**
- `Id` (`Guid`) — identifiant stable
- `Name` (`string`) — 1–120
- `LogicalPath` (`string`) — chemin relatif, le fichier est le sprite
- `PlacementId` (`string`) — id prefab, minuscules et tirets
- `FootprintWidthTiles` (`int`) — 1–32 cases
- `FootprintHeightTiles` (`int`) — 1–32 cases
- `WidthPixels` (`int`) — largeur image, > 0
- `HeightPixels` (`int`) — hauteur image, > 0
- `Sha256Hex` (`string`) — 64 hex
- `PngBytes` (`byte[]?`) — PNG publié
- `SpriteFileName` (`string`) — nom de fichier du chemin
- `MaxNameLength` (`int`) — 120
- `MaxLogicalPathLength` (`int`) — 500
- `MaxPlacementIdLength` (`int`) — 64
- `MinFootprintTiles` (`int`) — 1
- `MaxFootprintTiles` (`int`) — 32

**Entrées :**
- —

**Sorties :**
- `definition` (`MapObjectDefinition`) — prop plaçable

**Exemple :**
```csharp
string placement = MapObjectDefinition.CreatePlacementId(id);
string sha = MapObjectDefinition.ComputeSha256Hex(png);
// Prop plaçable, distinct d’un objet d’inventaire.
// mapobjectdefinition → MapObjectDefinition
```
