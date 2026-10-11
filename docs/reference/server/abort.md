# abort

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Libère une réservation dont le spawn a échoué ou quitté la zone.

*Source : `MobSpawnZoneKeeper.Abort`*

**Signature :** `abort(reservationId) → void`

**Entrées :**
- `reservationId` (`Guid`) — ordre non confirmé

**Sorties :**
- `effet` (`void`) — la place redevient libre au prochain Tick

**Exemple :**
```csharp
keeper.Abort(order.ReservationId);
// Libère une réservation dont le spawn a échoué ou quitté la zone.
// abort(reservationId) → void
```
