# commit

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Attache l’instance vivante à la réservation.

*Source : `MobSpawnZoneKeeper.Commit`*

**Signature :** `commit(reservationId, instanceId) → void`

**Entrées :**
- `reservationId` (`Guid`) — ordre émis par Tick
- `instanceId` (`Guid`) — monstre créé

**Sorties :**
- `effet` (`void`) — la place compte comme vivante

**Exemple :**
```csharp
keeper.Commit(order.ReservationId, instanceId);
// Attache l’instance vivante à la réservation.
// commit(reservationId, instanceId) → void
```
