# shouldrejectloginasync

← [Server](README.md) · [Référence](../README.md)

Décide si un login (ou flux auth voisin) doit être refusé pour maintenance.

*Source : `MaintenanceService.ShouldRejectLoginAsync`* · Tip : `d6e59759` · #31

**Signature :** `shouldrejectloginasync(accountId?, cancellationToken?)`

**Entrées :**
- `accountId` (`Guid?`) — compte connu si déjà résolu
- `cancellationToken` — annulation

**Sorties :**
- (`Task<bool>`) — `true` = refuser avec message maintenance

**Refus / bypass :**
- maintenance off → `false`
- `AllowOperators` + compte dans `IOperatorDirectory` → `false` (laisse passer)
