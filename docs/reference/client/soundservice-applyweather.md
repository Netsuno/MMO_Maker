# soundservice-applyweather

← [Client](README.md) · [Référence](../README.md)

Hook météo mute-friendly (pas de nouveau moteur / WAV).

*Source : `SoundService.ApplyWeather`* · Tip : `a6edd821` · #30

**Signature :** `applyweather(plan)`

**Entrées :**
- `plan` (`WeatherOverlayPlan`)

**Sorties :**
- (`bool`) — `WeatherAudio.ShouldPlayAmbience(plan, mixer)`

**Exemple :**
```csharp
bool ambienceOk = sound.ApplyWeather(plan);
// false si mute / gain 0 (WeatherAudio gate)
// Hook météo mute-friendly (pas de nouveau moteur / WAV)
```
