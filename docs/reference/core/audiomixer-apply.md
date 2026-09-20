# audiomixer-apply

← [Core](README.md) · [Référence](../README.md)

Applique volume, mute et toggle musique en une fois.

*Source : `AudioMixer.Apply`* · Tip : `d6e59759` · #28

**Signature :** `apply(volumePercent, muted, musicEnabled)`

**Entrées :**
- `volumePercent` (`int`) — clampé 0–100
- `muted` (`bool`) — mute explicite
- `musicEnabled` (`bool`) — opt-in boucle musique

**Sorties :**
- état mixer mis à jour (`VolumePercent`, `MuteRequested`, `MusicEnabled`)

**Refus :** —
*(clamp silencieux sur le volume)*
