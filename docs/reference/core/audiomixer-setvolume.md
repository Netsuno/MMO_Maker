# audiomixer-setvolume

← [Core](README.md) · [Référence](../README.md)

Règle le volume maître (pourcentage).

*Source : `AudioMixer.SetVolume`* · Tip : `a6edd821` · #28

**Signature :** `setvolume(percent)`

**Entrées :**
- `percent` (`int`) — clampé 0–100

**Sorties :**
- `VolumePercent` mis à jour ; `Gain` / `IsMuted` dérivés

**Exemple :**
```csharp
mixer.SetVolume(80); // clamp 0–100
// Règle le volume maître (pourcentage)
// VolumePercent mis à jour ; Gain / IsMuted dérivés
```
