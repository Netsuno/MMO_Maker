# formatstatus

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Phrase de barre d’état pour l’outil Zone.

*Source : `MobSpawnZoneLabels.FormatStatus`*

**Signature :** `formatstatus(zoneName) → string`

**Entrées :**
- `zoneName` (`string?`) — nom sélectionné

**Sorties :**
- `status` (`string`) — inclut la touche Z

**Exemple :**
```csharp
string status = MobSpawnZoneLabels.FormatStatus(zone.Name);
// Phrase de barre d’état pour l’outil Zone.
// formatstatus(zoneName) → string
```
