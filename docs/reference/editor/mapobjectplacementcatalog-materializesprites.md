# mapobjectplacementcatalog-materializesprites

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Écrit les PNG publiés dans le dossier Prefabs.

*Source : `MapObjectPlacementCatalog.MaterializeSprites`*

**Signature :** `materializesprites(published, directory) → int`

**Entrées :**
- `published` (`IEnumerable<MapObjectDefinition>?`) — objets avec PNG
- `directory` (`string?`) — dossier Prefabs

**Sorties :**
- `written` (`int`) — fichiers écrits, 0 si dossier vide

**Refus :**
- nom de sprite avec `..` ou un séparateur ignoré

**Exemple :**
```csharp
int n = MapObjectPlacementCatalog.MaterializeSprites(published, directory);
// Écrit les PNG publiés dans le dossier Prefabs.
// materializesprites(published, directory) → int
```
