# mobspawnzoneedit-tryremove

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Retire une zone du document.

*Source : `MobSpawnZoneEdit.TryRemove`*

**Signature :** `tryremove(document, id) → bool`

**Entrées :**
- `document` (`MobSpawnZoneDocument`) — porteur
- `id` (`Guid`) — zone

**Sorties :**
- `removed` (`bool`) — faux si l’id est absent

**Exemple :**
```csharp
bool removed = MobSpawnZoneEdit.TryRemove(document, id);
// Retire une zone du document.
// tryremove(document, id) → bool
```
