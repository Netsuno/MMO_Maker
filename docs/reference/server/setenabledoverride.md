# setenabledoverride

← [Server](README.md) · [Référence](../README.md)

Force le drapeau maintenance in-process (tests / ops).

*Source : `MaintenanceService.SetEnabledOverride`* · Tip : `a6edd821` · #31

**Signature :** `setenabledoverride(enabled?)`

**Entrées :**
- `enabled` (`bool?`) — `true`/`false` force ; `null` revient à config / env / fichier

**Sorties :**
- override stocké ; log informationnel

**Exemple :**
```csharp
maintenance.SetEnabledOverride(true);  // force ON
maintenance.SetEnabledOverride(null);  // revient à config/env
// Force le drapeau maintenance in-process (tests / ops)
```

**Note :** n’équivaut pas à un drain de sessions déjà connectées.
