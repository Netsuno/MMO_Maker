# STATUS — Client UI Kenney pack

| Champ | Valeur |
| --- | --- |
| **Chantier** | Brancher le pack UI Kenney (RPG Expansion) + icônes game-icons sur le HUD live |
| **Propriétaire** | Netsun |
| **Statut** | Assets + crédits + fils HUD existants (frames / slots / menu / hotbar / barres / CTA) |
| **Base** | `main` @ `e5b298d` (ou plus récent) |
| **Branche** | `cursor/client-ui-kenney-pack-79b6` |
| **PR** | Draft vers `main` — **pas de merge** |

Protocole inchangé. Mouvement / `InputService.IsTextInputFocus` / timer 16 ms **non réécrits**.
Jetons DA inchangés (`#161C28` / `#C9A227`) — tint `ImageAttributes` dans `UiTheme`, pas de nouveau DA.
`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel` restent surfaces SHA Phase 8.

Aucun claim de publication publique.

---

## Mapping (fils réels uniquement)

| Asset | Chemin | Fil live |
| --- | --- | --- |
| `panel_brown.png` | `Frog.Client/Assets/Ui/frames/` | `HudModulePanel` (Status, Minimap, QuestTracker, Chat) — 9-slice + filet or DA |
| `panelInset_brown.png` | `Frog.Client/Assets/Ui/frames/` | `HudHotbar` chrome intérieur |
| `buttonSquare_brown.png` (+ pressed copié) | `Frog.Client/Assets/Ui/slots/` | Cases `HudHotbar` (chiffres 1–0 conservés) |
| `buttonRound_brown.png` | `Frog.Client/Assets/Ui/menu/` | Pills `HudMenuRing` (libellés Perso/Inv/Quêtes/Carte/Options conservés) |
| `barRed_*` / `barBlue_*` / `barBack_*` | `Frog.Client/Assets/Ui/bars/` | HP / MP `HudStatusModule` (pas de barre XP) |
| `buttonLong_brown.png` | `Frog.Client/Assets/Ui/chrome/` | CTA « Envoyer chat » (`HudChatDock.AttachInputs`) |
| `arrowBrown_*` | `Frog.Client/Assets/Ui/chrome/` | **Livrés, non branchés** — `DialoguePanel` est SHA Phase 8 |
| `walk` / `backpack` / `scroll-unfurled` / `treasure-map` / `cog` | `Frog.Client/Assets/Ui/icons/menu/` | MenuRing (Perso / Inv / Quêtes / Carte / Options) |
| `broadsword` / `fire-spell-cast` / `hand` | `Frog.Client/Assets/Ui/icons/hotbar/` | Hotbar slots 1–3 (mêlée / sort / interagir) |

Tint : cadres / slots / pills / fond de barre → `UiTheme.CreatePanelTintAttributes()` vers `#161C28`.
Icônes blanches → `UiTheme.CreateGoldTintAttributes()` vers `#C9A227`.

Crédits : [THIRD_PARTY.md](../../../THIRD_PARTY.md), [CREDITS.md](../../../CREDITS.md), copie runtime `Frog.Client/Assets/Ui/CREDITS.md`.

---

## Hors scope

- Pas de second panneau / stub folklore (`ChatPanel`, `StatusBar`, `MiniMap`, `DialogForm`).
- Pas de restyle SHA 02–04.
- Pas de changement d’input, d’opcodes, ni de tokens DA.
