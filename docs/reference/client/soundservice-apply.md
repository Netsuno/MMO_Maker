# soundservice-apply

← [Client](README.md) · [Référence](../README.md)

Applique `UserSettings` (volume / mute / musique) puis synchronise la boucle.

*Source : `SoundService.Apply`* · Tip : `d6e59759` · #28

**Signature :** `apply(settings)`

**Entrées :**
- `settings` (`UserSettings`) — `VolumePercent`, `AudioMuted`, `MusicEnabled`

**Sorties :**
- settings clampées / réécrites ; `SyncMusic()` appelé

**Refus :**
- `settings` null → exception
