# mobspawnzoneedit-tryaddentry

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ajoute un monstre dans la zone.

*Source : `MobSpawnZoneEdit.TryAddEntry`*

**Signature :** `tryaddentry(zone, monsterId, label, quantity, respawnSeconds, out entry, out error) → bool`

**Entrées :**
- `zone` (`MobSpawnZone`) — cible
- `monsterId` (`Guid`) — monstre
- `label` (`string?`) — libellé
- `quantity` (`int`) — 1–99
- `respawnSeconds` (`int`) — 0–86400

**Sorties :**
- `ok` (`bool`) — ligne ajoutée
- `entry` (`out MobSpawnEntry`) — créée
- `error` (`out string?`) — motif

**Refus :**
- 16 lignes déjà là
- monstre vide
- quantité ou délai hors plage

**Exemple :**
```csharp
MobSpawnZoneEdit.TryAddEntry(zone, monsterId, "Loup", 3, 20, out var entry, out var error);
// Ajoute un monstre dans la zone.
// tryaddentry(zone, monsterId, label, quantity, respawnSeconds, out entry, out error) → bool
```
