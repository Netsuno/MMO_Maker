# mapcanvas-mobspawnzoneschanged

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Signal après un changement des zones.

*Source : `MapCanvas.MobSpawnZonesChanged`*

**Signature :** `mobspawnzoneschanged → event Action?`

**Entrées :**
- —

**Sorties :**
- `event` (`Action?`) — abonnés prévenus

**Exemple :**
```csharp
canvas.MobSpawnZonesChanged += () => { };
// Signal après un changement des zones.
// mobspawnzoneschanged → event Action?
```
