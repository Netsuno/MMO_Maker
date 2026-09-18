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
