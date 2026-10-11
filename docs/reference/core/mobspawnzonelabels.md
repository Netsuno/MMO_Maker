# mobspawnzonelabels

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Libellés français de l’outil Zone.

*Source : `MobSpawnZoneLabels`*

**Signature :** `mobspawnzonelabels → constantes`

**Membres :**
- `ToolName` (`string`) — Zone
- `PanelTitle` (`string`) — Zone de monstres
- `EditMenu` (`string`) — Outil zone de monstres
- `Respawn` (`string`) — Réapparition (s)

**Entrées :**
- —

**Sorties :**
- `labels` (`MobSpawnZoneLabels`) — chaînes françaises

**Exemple :**
```csharp
string status = MobSpawnZoneLabels.FormatStatus(zone.Name);
string line = MobSpawnZoneLabels.FormatEntry(entry);
// Libellés français de l’outil Zone.
// mobspawnzonelabels → constantes
```
