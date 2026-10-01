# tryapplyreportedpixelposition

← [Server](README.md) · [Référence](../README.md)

Valide et applique une position pixel rapportée par le client.

*Source : `MovementService.TryApplyReportedPixelPosition`*

**Signature :** `tryapplyreportedpixelposition(session, reportedPixelX, reportedPixelY)`

**Entrées :**
- `session` (`Session`) — session
- `reportedPixelX` (`int`) — X pixel
- `reportedPixelY` (`int`) — Y pixel

**Sorties :**
- (`bool`) — accepté
- `errorMessage` (`string`) — si refus

**Exemple :**
```csharp
if (!movement.TryApplyReportedPixelPosition(session, reportedPixelX: 320, reportedPixelY: 240, out var error))
    return;
// Valide et applique une position pixel rapportée par le client
```
