# setenabledoverride

← [Server](README.md) · [Référence](../README.md)

Force le drapeau maintenance in-process (tests / ops).

*Source : `MaintenanceService.SetEnabledOverride`* · Tip : `d6e59759` · #31

**Signature :** `setenabledoverride(enabled?)`

**Entrées :**
- `enabled` (`bool?`) — `true`/`false` force ; `null` revient à config / env / fichier

**Sorties :**
- override stocké ; log informationnel

**Note :** n’équivaut pas à un drain de sessions déjà connectées.
