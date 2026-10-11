# composedtilesetdefinition

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Tileset fait de tuiles 48×48 choisies, dans l’ordre de la palette.

*Source : `ComposedTilesetDefinition.Validate`, `Normalize`*

**Signature :** `composedtilesetdefinition → ComposedTilesetDefinition`

**Membres :**
- `Id` (`Guid`) — identifiant stable
- `Name` (`string`) — 1–120 caractères
- `LogicalPath` (`string`) — chemin relatif unique, max 500
- `Tiles` (`List<ComposedTileRef>`) — ordre de palette, au moins une
- `MaxNameLength` (`int`) — 120
- `MaxLogicalPathLength` (`int`) — 500
- `MaxDisplayNameLength` (`int`) — 120

**Entrées :**
- —

**Sorties :**
- `definition` (`ComposedTilesetDefinition`) — nom, chemin, tuiles

**Exemple :**
```csharp
definition.Normalize();
if (!definition.Validate(out var error)) return;
// Tileset fait de tuiles 48×48 choisies, dans l’ordre de la palette.
// composedtilesetdefinition → ComposedTilesetDefinition
```
