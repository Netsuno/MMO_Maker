# audiomixer-setmusicenabled

← [Core](README.md) · [Référence](../README.md)

Active l’opt-in musique (défaut off côté settings client).

*Source : `AudioMixer.SetMusicEnabled`* · Tip : `a6edd821` · #28

**Signature :** `setmusicenabled(enabled)`

**Entrées :**
- `enabled` (`bool`)

**Sorties :**
- `MusicEnabled` ; `MusicGain` = 0 si off

**Exemple :**
```csharp
mixer.SetMusicEnabled(true);
// Active l’opt-in musique (défaut off côté settings client)
// MusicEnabled ; MusicGain = 0 si off
```
