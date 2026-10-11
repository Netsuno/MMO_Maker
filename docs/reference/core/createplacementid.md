# createplacementid

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Fabrique l’identifiant de pose `obj-` plus le Guid compact.

*Source : `MapObjectDefinition.CreatePlacementId`*

**Signature :** `createplacementid(id) → string`

**Entrées :**
- `id` (`Guid`) — vide → un nouveau Guid

**Sorties :**
- `placementId` (`string`) — préfixe obj-

**Exemple :**
```csharp
string placementId = MapObjectDefinition.CreatePlacementId(id);
// Fabrique l’identifiant de pose `obj-` plus le Guid compact.
// createplacementid(id) → string
```
