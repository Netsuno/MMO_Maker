# trygetwarpdestination

← [Server](README.md) · [Référence](../README.md)

Résout la destination d’un warp sur une tuile.

*Source : `MapService.TryGetWarpDestination`*

**Signature :** `trygetwarpdestination(mapId, tileX, tileY)`

**Entrées :**
- `mapId` (`int`) — carte
- `tileX` (`int`) — colonne
- `tileY` (`int`) — ligne

**Sorties :**
- (`bool`) — warp présent
- `targetMapId` (`int`) — carte cible
- `targetX` (`int`) — tuile X
- `targetY` (`int`) — tuile Y

**Exemple :**
```csharp
if (mapService.TryGetWarpDestination(mapId: 1, tileX: 5, tileY: 9, out var destMap, out var tx, out var ty))
    movement.TryTeleportToTile(session, destMap, tx, ty, out _);
// Résout la destination d’un warp sur une tuile
```
