# tryteleporttotile

← [Server](README.md) · [Référence](../README.md)

Téléporte le personnage sur une tuile (carte/x/y).

*Source : `MovementService.TryTeleportToTile`*

**Signature :** `tryteleporttotile(session, targetMapId, tileX, tileY)`

**Entrées :**
- `session` (`Session`)
- `targetMapId` (`int`) — carte cible
- `tileX` (`int`) — colonne
- `tileY` (`int`) — ligne

**Sorties :**
- (`bool`) — succès
- `errorMessage` (`string`) — si refus

**Exemple :**
```csharp
if (!movement.TryTeleportToTile(session, targetMapId: 2, tileX: 10, tileY: 4, out var error))
    return;
// Téléporte le personnage sur une tuile (carte/x/y)
```
