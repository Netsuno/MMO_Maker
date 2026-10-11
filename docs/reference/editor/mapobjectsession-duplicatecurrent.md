# mapobjectsession-duplicatecurrent

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Duplique le courant en nouveau brouillon.

*Source : `MapObjectWorkspaceSession.DuplicateCurrent`*

**Signature :** `duplicatecurrent() → MapObjectDefinition`

**Entrées :**
- —

**Sorties :**
- `copy` (`MapObjectDefinition`) — nouvel id, nom suffixé

**Refus :**
- aucun courant ouvert

**Exemple :**
```csharp
var copy = session.DuplicateCurrent();
// Duplique le courant en nouveau brouillon.
// duplicatecurrent() → MapObjectDefinition
```
