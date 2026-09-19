# STATUS — Client UI Kenney pack

| Champ | Valeur |
| --- | --- |
| **Chantier** | Brancher le kit DA Kenney + game-icons sur le HUD live |
| **Propriétaire** | Netsun |
| **Statut** | Kit `Assets/Ui/` + crédits + fils HUD existants |
| **Base** | `main` @ `e5b298d` |
| **Branche** | `cursor/client-ui-kenney-pack-79b6` |
| **PR** | Draft vers `main` — **pas de merge** |

Protocole inchangé. Mouvement / `InputService.IsTextInputFocus` / timer 16 ms **non réécrits**.
Jetons DA inchangés (`#161C28` / `#C9A227`) — tint `ImageAttributes` dans `UiTheme`, pas de nouveau DA.
`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel` restent surfaces SHA Phase 8.

Aucun claim de publication publique.

Kit source : archive DA `frog-ui-kit.tar.gz` → `Frog.Client/Assets/Ui/` (`KIT-SELECTION.md`).

---

## Mapping (fils réels uniquement)

| Asset kit | Source Kenney / game-icons | Fil live |
| --- | --- | --- |
| `frames/panel.png` | `panel_brown.png` | `HudModulePanel` (Status, Minimap, QuestTracker, Chat) |
| `frames/panel_inset.png` | `panelInset_brown.png` | `HudHotbar` chrome intérieur |
| `slots/slot.png` (+ `_pressed`) | `buttonSquare_brown.png` | Cases `HudHotbar` (chiffres 1–0 conservés) |
| `menu/btn_round.png` | `buttonRound_brown.png` | Pills `HudMenuRing` (libellés conservés) |
| `bars/hp_*` / `mp_*` / `track_*` | `barRed_*` / `barBlue_*` / `barBack_*` | HP / MP `HudStatusModule` |
| `bars/mp_mid.png` | `barBlue_horizontalBlue.png` | MP mid (nom Kenney) |
| `bars/xp_*` | `barYellow_*` | **Livrés, non branchés** (max XP absent du fil) |
| `chrome/btn_long.png` | `buttonLong_brown.png` | CTA « Envoyer chat » |
| `chrome/arrow_*.png` | `arrowBrown_*` | **Livrés, non branchés** — `DialoguePanel` SHA Phase 8 |
| `menu/icon_perso` / `_inv` / `_quetes` / `_carte` / `_options` | walk / backpack / scroll-unfurled / treasure-map / cog | MenuRing |
| `hotbar/icon_melee` / `_spell` / `_interact` | broadsword / fire-spell-cast / hand | Hotbar slots 1–3 |

Tint : cadres / slots / pills / piste → `UiTheme.CreatePanelTintAttributes()` vers `#161C28`.
Icônes → `UiTheme.CreateGoldTintAttributes()` vers `#C9A227`.

Crédits : [THIRD_PARTY.md](../../../THIRD_PARTY.md), [CREDITS.md](../../../CREDITS.md), runtime `Frog.Client/Assets/Ui/THIRD_PARTY.md`.

---

## Hors scope

- Pas de second panneau / stub folklore (`ChatPanel`, `StatusBar`, `MiniMap`, `DialogForm`).
- Pas de restyle SHA 02–04.
- Pas de changement d’input, d’opcodes, ni de tokens DA.
