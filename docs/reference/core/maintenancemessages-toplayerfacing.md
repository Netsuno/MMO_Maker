# maintenancemessages-toplayerfacing

← [Core](README.md) · [Référence](../README.md)

Mappe un signal « maintenance » vers le libellé joueur.

*Source : `MaintenanceMessages.ToPlayerFacing`* · Tip : `d6e59759` · #31

**Signature :** `toplayerfacing(raw?)`

**Entrées :**
- `raw` (`string?`) — message serveur / réseau

**Sorties :**
- (`string`) — texte joueur si `IsMaintenanceSignal`, sinon `raw`

Constante wire : `LoginRejected` = `Serveur en maintenance. Reessayez plus tard.`
