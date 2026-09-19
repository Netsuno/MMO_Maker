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
