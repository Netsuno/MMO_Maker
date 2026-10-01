# audiomixer-setmuted

← [Core](README.md) · [Référence](../README.md)

Active ou coupe le mute explicite (distinct du slider à 0).

*Source : `AudioMixer.SetMuted`* · Tip : `a6edd821` · #28

**Signature :** `setmuted(muted)`

**Entrées :**
- `muted` (`bool`)

**Sorties :**
- `MuteRequested` ; `IsMuted` = mute **ou** volume ≤ 0

**Exemple :**
```csharp
mixer.SetMuted(false);
// Active ou coupe le mute explicite (distinct du slider à 0)
// MuteRequested ; IsMuted = mute ou volume ≤ 0
```
