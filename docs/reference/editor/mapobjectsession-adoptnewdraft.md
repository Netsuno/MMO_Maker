# mapobjectsession-adoptnewdraft

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Prend un brouillon neuf, révision 0, sale.

*Source : `MapObjectWorkspaceSession.AdoptNewDraft`*

**Signature :** `adoptnewdraft(definition) → void`

**Entrées :**
- `definition` (`MapObjectDefinition`) — contenu

**Sorties :**
- `effet` (`void`) — CurrentId null, IsDirty vrai

**Exemple :**
```csharp
session.AdoptNewDraft(definition);
// Prend un brouillon neuf, révision 0, sale.
// adoptnewdraft(definition) → void
```
