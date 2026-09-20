# audiomixer-setmuted

← [Core](README.md) · [Référence](../README.md)

Active ou coupe le mute explicite (distinct du slider à 0).

*Source : `AudioMixer.SetMuted`* · Tip : `d6e59759` · #28

**Signature :** `setmuted(muted)`

**Entrées :**
- `muted` (`bool`)

**Sorties :**
- `MuteRequested` ; `IsMuted` = mute **ou** volume ≤ 0
