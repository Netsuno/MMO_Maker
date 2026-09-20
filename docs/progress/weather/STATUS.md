# STATUS — Weather / world presentation MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Overlay météo léger (teinte + traits) + trailer `weatherKind` sur opcode **74** |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — overlay + F8 debug |
| **Merge** | [PR #30](https://github.com/Netsuno/MMO_Maker/pull/30) → `285162fcdbe918b8d9f0f36c373161946aca9dfc` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` |
| **CI (tip miroir)** | [35542485239](https://github.com/Netsuno/MMO_Maker/actions/runs/35542485239) **SUCCESS** (re-pin après cancel cascade merge) |
| **Protocole** | Version **11** — opcode `EnvironmentStatePush` **74** inchangé ; trailer additif |

---

## Avant → après

| Surface | Avant | Après |
| --- | --- | --- |
| Carte | tuiles / sprites seuls | + teinte + ≤12 traits pluie (`WeatherOverlayRenderer`, **internal**) |
| Kind | profil Guid seul | kind publié **ou** catalogue **ou** F8 |
| Wire 74 | cœur 38 octets | + trailer optionnel `u8 len + UTF-8` (plafond 32) |
| Audio météo | — | hook mute-friendly via `SoundService.ApplyWeather` (pas de WAV météo) |

---

## Ce qui marche (MVP)

1. **Catalogue** — clear / rain / fog + IDs démo / Phase 8 connus (`WeatherCatalog` + `WeatherResolver`).
2. **F8** — cycle debug Auto → Clair → Pluie → Brouillard (Aide F1 le rappelle).
3. **Fil additif** — parseurs 4-champs ignorent le surplus ; pas de bump version.
4. **Mute** — pluie/brouillard *voudraient* ambiance ; muet ou volume 0 = silence.

---

## Hors scope

- VFX particules, neige, orage, vent.
- Nouveau moteur audio / WAV météo.
- #29 fluidité mouvement (doc Référence-Core plus tard, **hors ce lot**).

---

## Tests

- `Frog.Tests/WeatherMvpTests.cs` — catalogue, resolver, trailer, mute gate, protocole 11.
- Linux / agent docs : pas de capture overlay. Placeholder `weather-01-f8-rain.png`.

## Honnêteté

- CI du commit merge #30 avait été **cancelled** (cascade) ; preuve tip = re-pin SUCCESS sur `d6e59759`.
- `WeatherOverlayRenderer` est **internal** — pas de fiche publique Client.


## Note tests (STATUS gate — ne pas retirer)

Ces phrases sont assertées par Frog.Tests StatusDoc_* :

- `pas de merge`
- `WeatherAudio`
- `FrogWireProtocol.Version`
- `reste 11`

pas de merge
WeatherAudio
FrogWireProtocol.Version
reste 11
