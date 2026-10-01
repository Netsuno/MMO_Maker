# maintenancemessages-toplayerfacing

← [Core](README.md) · [Référence](../README.md)

Mappe un signal « maintenance » vers le libellé joueur.

*Source : `MaintenanceMessages.ToPlayerFacing`* · Tip : `a6edd821` · #31

**Signature :** `toplayerfacing(raw?)`

**Entrées :**
- `raw` (`string?`) — message serveur / réseau

**Sorties :**
- (`string`) — texte joueur si `IsMaintenanceSignal`, sinon `raw`

**Exemple :**
```csharp
string msg = MaintenanceMessages.ToPlayerFacing(raw);
// Mappe un signal « maintenance » vers le libellé joueur
// (string) — texte joueur si IsMaintenanceSignal, sinon raw
```

Constante wire : `LoginRejected` = `Serveur en maintenance. Reessayez plus tard.`
