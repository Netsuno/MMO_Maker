# mobspawnorder

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ordre d’apparition réalisé par SpawnMonster, pas un second type de monstre.

*Source : `MobSpawnOrder`*

**Signature :** `mobspawnorder(reservationId, mapId, runtimeMapId, zoneId, entryId, monsterId, tileX, tileY) → MobSpawnOrder`

**Membres :**
- `ReservationId` (`Guid`) — réservation
- `MapId` (`Guid`) — carte
- `RuntimeMapId` (`int`) — carte runtime
- `ZoneId` (`Guid`) — zone
- `EntryId` (`Guid`) — ligne
- `MonsterId` (`Guid`) — monstre
- `TileX` (`int`) — colonne
- `TileY` (`int`) — ligne

**Entrées :**
- `reservationId` (`Guid`) — place réservée jusqu’au commit
- `monsterId` (`Guid`) — monstre déjà défini
- `tileX` (`int`) — case dans le rectangle
- `tileY` (`int`) — case dans le rectangle

**Sorties :**
- `order` (`MobSpawnOrder`) — immuable

**Exemple :**
```csharp
MobSpawnOrder order = orders[0];
var (px, py) = WorldMetrics.TileCenterToPixels(order.TileX, order.TileY);
// Ordre d’apparition réalisé par SpawnMonster, pas un second type de monstre.
// mobspawnorder(reservationId, mapId, runtimeMapId, zoneId, entryId, monsterId, tileX, tileY) → MobSpawnOrder
```
