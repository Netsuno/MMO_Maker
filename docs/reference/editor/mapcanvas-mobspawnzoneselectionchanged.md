# mapcanvas-mobspawnzoneselectionchanged

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Signal après un changement de sélection de zone.

*Source : `MapCanvas.MobSpawnZoneSelectionChanged`*

**Signature :** `mobspawnzoneselectionchanged → event Action?`

**Entrées :**
- —

**Sorties :**
- `event` (`Action?`) — abonnés prévenus

**Exemple :**
```csharp
canvas.MobSpawnZoneSelectionChanged += () => { };
// Signal après un changement de sélection de zone.
// mobspawnzoneselectionchanged → event Action?
```
