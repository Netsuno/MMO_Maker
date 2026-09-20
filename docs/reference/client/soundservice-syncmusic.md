# soundservice-syncmusic

← [Client](README.md) · [Référence](../README.md)

Aligne la boucle musique stub sur l’état mixer (opt-in + mute).

*Source : `SoundService.SyncMusic`* · Tip : `d6e59759` · #28

**Signature :** `syncmusic()`

**Entrées :** —

**Sorties :**
- (`bool`) — résultat de `AudioMixer.Play(MusicLoop)` (stop si inaudible)
