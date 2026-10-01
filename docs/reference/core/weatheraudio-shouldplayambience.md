# weatheraudio-shouldplayambience

← [Core](README.md) · [Référence](../README.md)

Gate mute-friendly : la météo *voudrait* une ambiance, mais refuse si muet / gain 0.

*Source : `WeatherAudio.ShouldPlayAmbience`* · Tip : `a6edd821` · #30 (+ #28)

**Signature :** `shouldplayambience(plan, mixer)`

**Entrées :**
- `plan` (`WeatherOverlayPlan`) — `WantsAmbience`
- `mixer` (`AudioMixer`)

**Sorties :**
- (`bool`) — ambiance autorisée (pas de WAV météo dans ce MVP)

**Exemple :**
```csharp
bool ok = WeatherAudio.ShouldPlayAmbience(plan, mixer);
// Gate mute-friendly : la météo *voudrait* une ambiance, mais refuse si muet / gain 0
// (bool) — ambiance autorisée (pas de WAV météo dans ce MVP)
```
