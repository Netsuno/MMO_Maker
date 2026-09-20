# STATUS — Audio / music MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Couche audio minimale : SFX clic + musique stub, mute / volume |
| **Propriétaire** | Netsun |
| **Statut** | Scaffolding client (playtest éditeur = même EXE) — **pas de merge** |
| **Base** | `main` @ `bc60186` (merge PR #26 prefab objects) |
| **Branche** | `cursor/audio-mvp` |
| **PR** | Draft [#28](https://github.com/Netsuno/MMO_Maker/pull/28) vers `main` — **pas de merge** |
| **Tip** | `c677462` |
| **CI** | [35533142555](https://github.com/Netsuno/MMO_Maker/actions/runs/35533142555) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — pas de bump |

---

## Livré

1. **Abstraction mute / volume** — `Frog.Core.Audio.AudioMixer` (gain linéaire 0–1, mute explicite **ou** slider à 0, musique derrière `MusicEnabled`). Backend optionnel `IAudioPlayback` ; tests = `RecordingAudioPlayback` (aucun device).
2. **Placeholders originaux CC0** — `Frog.Client/Assets/Audio/ui-click.wav` + `music-loop.wav` générés par `tools/generate-audio-placeholders.py` (PCM 16-bit mono 22.05 kHz). Pas de téléchargement, pas d’assets arrachés.
3. **Client** — `SoundService` applique `UserSettings.VolumePercent` / `AudioMuted` / `MusicEnabled`. Lecture Windows via `System.Media.SoundPlayer` + gain PCM (`WavPcm.ScaleAmplitude`). **Zéro NuGet audio.**
4. **Clic UI** — `MainShellForm.OpenOptions` appelle `PlayUiClick()` (bouton Options + pill HUD Options).
5. **Musique stub** — boucle `PlayLooping` uniquement si Options → Son → « Musique (boucle placeholder) » est cochée **et** non muet. Défaut **off** (playtest / smoke silencieux).
6. **Options** — slider volume existant + cases Muet / Musique, persistés dans `client-settings.json` (champs additifs, schéma 1).

Playtest éditeur : le client lancé reprend le même JSON LocalAppData ; pas de second moteur son dans `Frog.Editor`.

---

## Hors scope (volontaire)

- Soundtrack complète, couches météo, pas, combat, chat, social.
- Fluidité de déplacement, protocole, panneaux sociaux.
- Mixage multi-voix, 3D, streaming MP3/OGG.

---

## Tests

- Unitaires Linux / Windows : `Frog.Tests/AudioMixerTests.cs` — mute, clamp volume, toggle musique, `WavPcm` gain, WAV in-repo, hooks client, ce STATUS.
- Smoke Windows : Options mute / musique persistés ; `PlayUiClick` vrai puis faux après mute (`Phase10ClientSettingsSmokeTests`).
- CI **SUCCESS** on `c677462` : `build-and-test` + `postgres-integration`.

---

## Preuve manuelle (Windows)

1. Lancer `Frog.Client`, Options → Son : un clic `ui-click.wav` à l’ouverture.
2. Cocher **Musique** + Enregistrer : pad 2 s en boucle.
3. Cocher **Muet** ou slider 0 : silence (SFX + musique).
