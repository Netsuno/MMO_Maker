# STATUS — Weather / world presentation MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Overlay météo léger (teinte + traits) branché sur `EnvironmentStatePush` / profils Phase 8 |
| **Propriétaire** | Netsun |
| **Statut** | Draft MVP — **pas de merge** |
| **Base** | `main` @ `8a51f5c` (merge #29 fluidity, inclut #28 audio / #27 social / #26 prefabs) |
| **Branche** | `cursor/weather-mvp` |
| **PR** | Draft [#30](https://github.com/Netsuno/MMO_Maker/pull/30) vers `main` — **pas de merge** |
| **Tip** | *(pin après CI)* |
| **CI** | Fix `1e40c8b` : `WeatherOverlayRenderer.Draw` est dans `MapViewRenderer` (chemin paint monde). |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — opcode `EnvironmentStatePush` **74** inchangé |

---

## Livré

1. **Catalogue** — `Frog.Core/Weather` : stub **clear / rain / fog** + IDs déjà publiés (démo P10 village/clair, faubourgs/pluie ; seed Phase 8 rain/clear ; smoke clair). `WeatherResolver` : kind publié → ID connu → clair. Toggle debug **F8** : Auto → Clair → Pluie → Brouillard.
2. **Fil additif, pas de bump** — `EnvironmentStatePush` historique **38 octets** toujours accepté. Trailer optionnel `u8 length + UTF-8 WeatherKind` (plafond 32) pour réutiliser `WeatherProfileDefinition.WeatherKind` du serveur. Parseurs 4-champs ignorent le surplus.
3. **Client** — `MapViewRenderer.Render` appelle `WeatherOverlayRenderer.Draw` en dernier (après tuiles / prefabs / sprites). Teinte + au plus **12** traits de pluie. Éclairage publié assombrit la teinte. Pluie idle : tick + `RedrawMap` (même chemin paint). `EnvironmentPanel` **non modifié** (exact-sha Phase 8 `04-environment.png`).
4. **Audio mute-friendly** — `WeatherAudio` + `SoundService.ApplyWeather` passent par `AudioMixer` (#28). Pluie / brouillard *voudraient* une ambiance ; **muet ou volume 0 = silence**. Pas de nouveau moteur, pas de WAV météo, pas de `AudioCue` ajouté.

---

## Hors scope (volontaire)

- Pipeline VFX particules, neige, orage, vent.
- Bump `FrogWireProtocol.Version`, HUD social, prefabs, fluidité / anim.
- Nouveau moteur audio, couches météo mixées, pas, combat.

---

## Tests

- `Frog.Tests/WeatherMvpTests.cs` — catalogue, resolver, lighting, particules bornées, gate mute, trailer additif vs 38 octets, câblage client, ce STATUS, protocole 11.
- Surfaces Phase 8 exact-sha (`EnvironmentPanel`) volontairement intactes.
- Linux this run: `dotnet test Frog.Tests` **677 passed** (filtre Weather **17 passed**).
- CI `1e40c8b` : unitaire Weather 16/17 (câblage) — fix poussé ; Windows editor / gameplay / Phase 8 smokes non relancés ici.

Linux / cet agent : pas de capture WinForms overlay. La teinte se voit en jeu (F8) sur Windows.
