# workingtileset-servertileetid

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Identifiant serveur d’une palette publiée. Vide : palette locale.

*Source : `WorkingTileset.ServerTilesetId`*

**Signature :** `servertileetid → Guid`

**Membres :**
- `ServerTilesetId` (`Guid`) — id publié, ou Empty

**Entrées :**
- —

**Sorties :**
- `id` (`Guid`) — `Guid.Empty` si la palette n’est pas publiée

**Exemple :**
```csharp
bool local = working.ServerTilesetId == Guid.Empty;
// Identifiant serveur d’une palette publiée. Vide : palette locale.
// servertileetid → Guid
```
