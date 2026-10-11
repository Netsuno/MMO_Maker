# mobspawnzoneedit-tryreplaceentry

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Remplace une ligne existante.

*Source : `MobSpawnZoneEdit.TryReplaceEntry`*

**Signature :** `tryreplaceentry(zone, entryId, monsterId, label, quantity, respawnSeconds, out error) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — cible
- `entryId` (`Guid`) — ligne
- `monsterId` (`Guid`) — monstre
- `quantity` (`int`) — 1–99
- `respawnSeconds` (`int`) — 0–86400

**Sorties :**
- `ok` (`bool`) — ligne mise à jour
- `error` (`out string?`) — motif

**Refus :**
- ligne absente
- quantité ou délai hors plage

**Exemple :**
```csharp
MobSpawnZoneEdit.TryReplaceEntry(zone, entryId, monsterId, "Loup", 2, 15, out var error);
// Remplace une ligne existante.
// tryreplaceentry(zone, entryId, monsterId, label, quantity, respawnSeconds, out error) → bool
```
