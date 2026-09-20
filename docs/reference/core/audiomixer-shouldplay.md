# audiomixer-shouldplay

← [Core](README.md) · [Référence](../README.md)

Indique si un cue est audible selon mute / volume / musique.

*Source : `AudioMixer.ShouldPlay`* · Tip : `d6e59759` · #28

**Signature :** `shouldplay(cue)`

**Entrées :**
- `cue` (`AudioCue`)

**Sorties :**
- (`bool`) — `GainFor(cue) > 0`
