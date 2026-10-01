# weatherresolver-cycle

← [Core](README.md) · [Référence](../README.md)

Avance le toggle debug F8 (Auto → Clair → Pluie → Brouillard → …).

*Source : `WeatherResolver.Cycle`* · Tip : `a6edd821` · #30

**Signature :** `cycle(current)`

**Entrées :**
- `current` (`WeatherDebugOverride`)

**Sorties :**
- (`WeatherDebugOverride`) — valeur suivante (mod 4)

**Exemple :**
```csharp
debug = WeatherResolver.Cycle(debug); // Auto → Clair → Pluie → Brouillard
// Avance le toggle debug F8 (Auto → Clair → Pluie → Brouillard → …)
// (WeatherDebugOverride) — valeur suivante (mod 4)
```
