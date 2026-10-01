# audiomixer-shouldplay

← [Core](README.md) · [Référence](../README.md)

Indique si un cue est audible selon mute / volume / musique.

*Source : `AudioMixer.ShouldPlay`* · Tip : `a6edd821` · #28

**Signature :** `shouldplay(cue)`

**Entrées :**
- `cue` (`AudioCue`)

**Sorties :**
- (`bool`) — `GainFor(cue) > 0`

**Exemple :**
```csharp
if (mixer.ShouldPlay(AudioCue.MusicLoop))
    mixer.Play(AudioCue.MusicLoop);
// Indique si un cue est audible selon mute / volume / musique
```
