# soundservice-apply

← [Client](README.md) · [Référence](../README.md)

Applique `UserSettings` (volume / mute / musique) puis synchronise la boucle.

*Source : `SoundService.Apply`* · Tip : `a6edd821` · #28

**Signature :** `apply(settings)`

**Entrées :**
- `settings` (`UserSettings`) — `VolumePercent`, `AudioMuted`, `MusicEnabled`

**Sorties :**
- settings clampées / réécrites ; `SyncMusic()` appelé

**Refus :**
- `settings` null → exception

**Exemple :**
```csharp
sound.Apply(settings); // VolumePercent / AudioMuted / MusicEnabled
sound.SyncMusic();
// Applique UserSettings (volume / mute / musique) puis synchronise la boucle
```
