# audiomixer-play

← [Core](README.md) · [Référence](../README.md)

Tente une lecture SFX ou musique via le backend optionnel.

*Source : `AudioMixer.Play`* · Tip : `a6edd821` · #28

**Signature :** `play(cue)`

**Entrées :**
- `cue` (`AudioCue`) — `UiClick` ou `MusicLoop`

**Sorties :**
- (`bool`) — `true` si lecture demandée au backend ; `false` si inaudible

**Refus :**
- gain ≤ 0 (mute, volume 0, ou musique off) → stop musique si cue = `MusicLoop`, retourne `false`

**Exemple :**
```csharp
if (!mixer.Play(AudioCue.UiClick))
    return; // gain ≤ 0
// Tente une lecture SFX ou musique via le backend optionnel
```
