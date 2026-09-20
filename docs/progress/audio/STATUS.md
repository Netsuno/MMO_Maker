# STATUS — Audio / music MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Couche audio minimale : SFX clic + musique stub, mute / volume |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — MVP Options Son + `SoundService` |
| **Merge** | [PR #28](https://github.com/Netsuno/MMO_Maker/pull/28) → `58884268d8b644e0f6bb0bb47e75101478eeb812` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` (main tip courant) |
| **Tip feature** | `c677462` (pin STATUS audio avant merge) |
| **CI (tip miroir)** | [35542485239](https://github.com/Netsuno/MMO_Maker/actions/runs/35542485239) **SUCCESS** |
| **Protocole** | `FrogWireProtocol.Version` **11** — pas de bump |

---

## Avant → après

| Surface | Avant | Après |
| --- | --- | --- |
| Volume Options | slider sans effet audible clair | `AudioMixer` gain linéaire 0–1 + mute |
| SFX | — | `ui-click.wav` via `PlayUiClick` (Options / HUD) |
| Musique | — | boucle stub opt-in (`MusicEnabled`, défaut **off**) |
| Persistance | volume seul | + `AudioMuted` / `MusicEnabled` dans `client-settings.json` |

---

## Ce qui marche (MVP)

1. **Core** — `Frog.Core.Audio.AudioMixer` : mute explicite **ou** slider 0 ; musique derrière `MusicEnabled`.
2. **Client** — `SoundService.Apply` / `PlayUiClick` / `SyncMusic` ; backend Windows `System.Media.SoundPlayer` + gain PCM. **Zéro NuGet audio.**
3. **Options → Son** — slider volume, cases **Muet (SFX + musique)** / **Musique (boucle placeholder)**.
4. Placeholders CC0 in-repo : `Frog.Client/Assets/Audio/ui-click.wav`, `music-loop.wav`.

Guide UI : [UI-CLIENT-Options.md](../phase-10-beta-release/guides/UI-CLIENT-Options.md).

---

## Hors scope (volontaire)

- Soundtrack complète, couches météo mixées, pas, combat, chat.
- Mixage multi-voix, 3D, streaming MP3/OGG.
- Installateur / CDN (voir #31 launcher stub).

---

## Tests

- Unitaires : `Frog.Tests/AudioMixerTests.cs`.
- Smoke Windows : mute / musique persistés (`Phase10ClientSettingsSmokeTests`).
- Linux / agent docs : pas de capture WinForms Options. Placeholders HTML + *Capture à venir*.

## Honnêteté

- Mergé ; tip docs = tip `main` courant (inclut #29–#33 après #28).
- Pas de métriques inventées ; pas de phrase READY / gate.
