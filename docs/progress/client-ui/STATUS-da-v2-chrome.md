# STATUS — Client UI DA v2 step 2 (window chrome)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Chrome fenêtres : titlebar 28–32, titre or, X rouge 20×20, padding 10–12, tabs or |
| **Propriétaire** | Netsun |
| **Statut** | Step 2 only — overlay Inventaire / Perso / Quêtes |
| **Base** | `main` @ `8eb980b` (merge PR #18 movement measure, includes #16 contrast + #17 skin v2) |
| **Branche** | `cursor/client-ui-da-v2-chrome` |
| **PR** | Draft vers `main` — **pas de merge** |

Protocole / gameplay / réseau / mouvement / skin / hotbar contrast / login / portrait status : **non touchés**.
Présentation + layout chrome seulement. TabControl overlay reste **360 px** (crops SHA Phase 8 324 px).

---

## Avant → après

| Surface | Avant | Après (tokens DA) |
| --- | --- | --- |
| Overlay fenêtres | `TabControl` 360 nu, pas de titlebar / X | `HudWindowChrome` : fond `bg.panel` `#161C28`, titlebar **30 px**, titre `accent.gold` `#C9A227` (or/blanc), padding **12** |
| Fermer | Esc / clic carte / Carte seulement | X rouge **20×20** `state.error` + texte `text.primary` `#F2F4F8` ; Esc inchangé |
| Tabs (déjà ouverts) | Tabs WinForms système Chat / Gameplay / Quêtes | Owner-draw or : **Inventaire** / **Quêtes** (+ Chat existant). Titlebar **Perso** si ouvert depuis le menu Perso |
| HUD modules | Titlebar compact 18 px | **Inchangé** (step 3 = portrait status) |

`UiTheme.Apply` repose `StyleWindowCloseButton` / `StyleGoldTabs` pour ne pas ramener le X au bouton or ni les tabs au thème plat.

---

## Hors scope (PR suivantes)

- Step 3 : portrait status HG
- Step 4 : layout 5 icônes menu
- Step 5 : login immersif
- Step 6 : remplacement frames Kenney
- Split contenu Inventaire ≠ Perso (même onglet Gameplay aujourd’hui)

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
