# Frog.Client — UI Options (Son)

← [Guides](README.md) · [MainShell](UI-CLIENT-MainShell.md) · [Référence Client](../../../reference/client/README.md) · [Audio STATUS](../../audio/STATUS.md)

Tip miroir : `d6e59759`. Feature #28 merge `5888426`. Propriétaire : **Netsun**.

Fenêtre **Options — FRoG** (`OptionsForm`). Ce guide documente l’onglet **Son** (volume / muet / musique). Autres onglets (Graphisme, Contrôles, Interface, Réseau) : inventaire court seulement.

---

## Ouverture

| Contrôle | Où | Fait quoi |
| --- | --- | --- |
| Bouton / pill **Options** | `MainShellForm` / HUD | Ouvre `OptionsForm` ; appelle `PlayUiClick()` si audible |
| Nav gauche | `OptionsForm` ListBox | Pages : Graphisme · **Son** · Contrôles · Interface · Réseau |

<!-- CAPTURE: assets/options-01-son.png -->
*Capture à venir : Options → Son — slider volume, label %, cases Muet et Musique, note WAV CC0.*

---

## Onglet Son — chaque contrôle

| Contrôle | Libellé UI | Rôle | API |
| --- | --- | --- | --- |
| `_volume` | slider 0–100 | Volume maître | → `SoundService.SetVolume` / `Apply` |
| `_lblVolume` | texte % | Affiche la valeur | — |
| `_chkMute` | **Muet (SFX + musique)** | Mute explicite (≠ seulement slider 0) | → `SetMuted` |
| `_chkMusic` | **Musique (boucle placeholder)** | Opt-in boucle `music-loop.wav` (défaut off) | → `SetMusicEnabled` / `SyncMusic` |

Note UI : *Clic UI : ui-click.wav. Musique : music-loop.wav (générés CC0, dans le dépôt).*

Enregistrement : bouton OK / Enregistrer de la fenêtre → `UserSettings` (`VolumePercent`, `AudioMuted`, `MusicEnabled`) → JSON LocalAppData → `SoundService.Apply`.

---

## Comportement attendu

| Action | Résultat |
| --- | --- |
| Ouvrir Options (non muet, volume > 0) | SFX clic possible |
| Cocher Musique + enregistrer | Boucle stub si non muet |
| Cocher Muet **ou** volume 0 | Silence SFX + musique |
| Décocher Musique | Arrêt boucle (`SyncMusic` / `Play` refuse) |

Météo (#30) : `ApplyWeather` respecte le même mute / gain — pas de WAV météo dédié.

---

## Autres onglets (inventaire)

| Onglet | Contenu (résumé) |
| --- | --- |
| Graphisme | Largeur / hauteur / maximisé / plein écran |
| Contrôles | Disposition AZERTY/QWERTY + rebind |
| Interface | Notes DPI / HUD |
| Réseau | Hôte / port (TLS = serveur) |

---

## Voir aussi

- [PLAYER_QUICKSTART](PLAYER_QUICKSTART.md) § Options Son
- Core : [audiomixer-apply](../../../reference/core/audiomixer-apply.md) · Client : [soundservice-playuiclick](../../../reference/client/soundservice-playuiclick.md)
