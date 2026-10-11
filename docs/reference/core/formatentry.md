# formatentry

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Formate une ligne de monstre pour la liste.

*Source : `MobSpawnZoneLabels.FormatEntry`*

**Signature :** `formatentry(entry) → string`

**Entrées :**
- `entry` (`MobSpawnEntry`) — ligne

**Sorties :**
- `text` (`string`) — nom ou id court, quantité, secondes

**Exemple :**
```csharp
string text = MobSpawnZoneLabels.FormatEntry(entry);
// Formate une ligne de monstre pour la liste.
// formatentry(entry) → string
```
