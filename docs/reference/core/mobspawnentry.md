# mobspawnentry

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ligne de zone : un monstre, une quantité vivante, un délai.

*Source : `MobSpawnEntry`*

**Signature :** `mobspawnentry → MobSpawnEntry`

**Membres :**
- `Id` (`Guid`) — ligne
- `MonsterId` (`Guid`) — NpcKind.Monster
- `Label` (`string`) — libellé, max 80
- `Quantity` (`int`) — 1–99 vivants
- `RespawnSeconds` (`int`) — 0–86400

**Entrées :**
- —

**Sorties :**
- `entry` (`MobSpawnEntry`) — ligne mutable

**Exemple :**
```csharp
var entry = new MobSpawnEntry { MonsterId = monsterId, Quantity = 2, RespawnSeconds = 30 };
// Ligne de zone : un monstre, une quantité vivante, un délai.
// mobspawnentry → MobSpawnEntry
```
