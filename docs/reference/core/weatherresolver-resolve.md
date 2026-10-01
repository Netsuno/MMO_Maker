# weatherresolver-resolve

← [Core](README.md) · [Référence](../README.md)

Résout un plan d’overlay (kind + teinte) depuis profil / état publié / F8.

*Source : `WeatherResolver.Resolve`* · Tip : `a6edd821` · #30

**Signature :** `resolve(weatherProfileId?, lightingLevel, publishedKind?, debug=Auto)`

**Entrées :**
- `weatherProfileId` (`Guid?`) — profil Phase 8 / démo
- `lightingLevel` (`byte`) — 0–255 (assombrit la teinte)
- `publishedKind` (`string?`) — trailer opcode 74 si présent
- `debug` (`WeatherDebugOverride`) — Auto / Clear / Rain / Fog

**Sorties :**
- (`WeatherOverlayPlan`) — kind, teinte, particules, flag ambiance

**Exemple :**
```csharp
var plan = WeatherResolver.Resolve(
    weatherProfileId, lightingLevel: 180, publishedKind: "rain",
    debug: WeatherDebugOverride.Auto);
```

**Priorité :** debug ≠ Auto → kind F8 ; sinon kind publié ; sinon catalogue profil ; sinon clair.
