# isblocked

← [Server](README.md) · [Référence](../README.md)

Indique si une tuile est bloquante pour le déplacement.

*Source : `MapService.IsBlocked`*

**Signature :** `isblocked(mapId, x, y)`

**Entrées :**
- `mapId` (`int`)
- `x` (`int`) — tuile X
- `y` (`int`) — tuile Y

**Sorties :**
- (`bool`) — `true` si bloqué

**Exemple :**
```csharp
if (mapService.IsBlocked(mapId: 1, x: 12, y: 8))
    return; // tuile bloquante
// Indique si une tuile est bloquante pour le déplacement
```
