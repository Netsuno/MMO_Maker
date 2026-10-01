# tryapplymove

← [Server](README.md) · [Référence](../README.md)

Applique un déplacement relatif grille après validations.

*Source : `MovementService.TryApplyMove`*

**Signature :** `tryapplymove(session, deltaX, deltaY)`

**Entrées :**
- `session` (`Session`) — session joueur
- `deltaX` (`sbyte`) — pas X
- `deltaY` (`sbyte`) — pas Y

**Sorties :**
- (`bool`) — mouvement accepté
- `errorMessage` (`string`) — motif si refus

**Refus :**
- hors limites / tuile bloquée / rate mouvement

**Exemple :**
```csharp
if (!movement.TryApplyMove(session, deltaX: 1, deltaY: 0, out var error))
    return; // hors limites / bloqué / rate
// Applique un déplacement relatif grille après validations
```
