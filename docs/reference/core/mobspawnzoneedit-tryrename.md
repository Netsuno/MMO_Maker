# mobspawnzoneedit-tryrename

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Renomme une zone (1–80 caractères).

*Source : `MobSpawnZoneEdit.TryRename`*

**Signature :** `tryrename(zone, name, out error) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — cible
- `name` (`string?`) — nouveau nom

**Sorties :**
- `ok` (`bool`) — nom appliqué
- `error` (`out string?`) — motif

**Refus :**
- nom vide ou > 80

**Exemple :**
```csharp
MobSpawnZoneEdit.TryRename(zone, "Clairière", out var error);
// Renomme une zone (1–80 caractères).
// tryrename(zone, name, out error) → bool
```
