# soundservice-syncmusic

← [Client](README.md) · [Référence](../README.md)

Aligne la boucle musique stub sur l’état mixer (opt-in + mute).

*Source : `SoundService.SyncMusic`* · Tip : `a6edd821` · #28

**Signature :** `syncmusic()`

**Entrées :** —

**Sorties :**
- (`bool`) — résultat de `AudioMixer.Play(MusicLoop)` (stop si inaudible)

**Exemple :**
```csharp
bool playing = sound.SyncMusic(); // AudioMixer.Play(MusicLoop)
// Aligne la boucle musique stub sur l’état mixer (opt-in + mute)
// (bool) — résultat de AudioMixer.Play(MusicLoop) (stop si inaudible)
```
