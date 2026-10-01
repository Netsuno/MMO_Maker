# weathercatalog-forkind

← [Core](README.md) · [Référence](../README.md)

Plan stub pour un kind normalisé (clear / rain / fog).

*Source : `WeatherCatalog.ForKind`* · Tip : `a6edd821` · #30

**Signature :** `forkind(kind?)`

**Entrées :**
- `kind` (`string?`) — alias FR/EN acceptés via `NormalizeKind`

**Sorties :**
- (`WeatherOverlayPlan`) — Clear / Rain / Fog (inconnu → Clear)

**Exemple :**
```csharp
var plan = WeatherCatalog.ForKind("pluie"); // → Rain (alias FR/EN)
// Plan stub pour un kind normalisé (clear / rain / fog)
// (WeatherOverlayPlan) — Clear / Rain / Fog (inconnu → Clear)
```
