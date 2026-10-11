# composedtilesetsession-duplicatecurrent

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Duplique le courant en nouveau brouillon.

*Source : `ComposedTilesetWorkspaceSession.DuplicateCurrent`*

**Signature :** `duplicatecurrent() → ComposedTilesetDefinition`

**Entrées :**
- —

**Sorties :**
- `copy` (`ComposedTilesetDefinition`) — nouvel id, nom suffixé

**Refus :**
- aucun courant ouvert

**Exemple :**
```csharp
var copy = session.DuplicateCurrent();
// Duplique le courant en nouveau brouillon.
// duplicatecurrent() → ComposedTilesetDefinition
```
