# STATUS — Client UI DA v2 step 3 (status portrait)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Status HG : portrait circulaire placeholder + nom + Lv + HP/MP (~280×72) |
| **Propriétaire** | Netsun |
| **Statut** | Step 3 only — layout planche, Kenney `bars/*` conservées |
| **Base** | `main` @ `78925b0` (merge PR #19 window chrome) |
| **Branche** | `cursor/client-ui-da-v2-status-portrait` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/20 vers `main` — **pas de merge** |
| **Tip** | `96191dc` |

Protocole / gameplay / réseau / mouvement / skin / hotbar contrast / chrome fenêtres / login / menu 5 icônes : **non touchés**.
Présentation + layout status HG seulement.

---

## Avant → après

| Surface | Avant | Après (tokens DA) |
| --- | --- | --- |
| Status HG | Nom + `Lv N` + barres, pas de portrait | Panneau `bg.panel` `#161C28` ; portrait circulaire placeholder **ø44** (plage 40–48) à gauche ; nom `text.primary` `#F2F4F8` + Lv + HP/MP |
| Emprise | 280×72 déjà | **280×72** conservée (`gap.hud` interne 8) |
| Portrait | — | Fill `bg.slot` `#0C1018` + filet or 1 px `accent.gold` `#C9A227` ; initiale ou buste placeholder (pas le skin monde) |
| Barres | Kenney `bars/*` HP/MP | **Inchangées** (couleurs natives + fallback `bar.hp` / `bar.mp`) |
| XP | Pas de barre (max absent du fil) | **Inchangé** |

`HudWindowChrome` / `HudHotbar` / `HudMenuRing` / login : hors ce PR (steps 1–2 déjà mergés ; 4–5 plus tard).

---

## Hors scope (PR suivantes)

- Step 4 : layout 5 icônes menu
- Step 5 : login immersif
- Step 6 : remplacement frames Kenney
- Portrait = vraie tête / skin monde (ici placeholder GDI seulement)

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.

Smoke Login-phase : ne pas utiliser `Control.Visible` / `PointToScreen` (ancêtres `_panelGame` masqués). Le split HG est colonne 0 portrait / colonne 1 nom+barres (`GetColumn(_body) == 1`, pas la table elle-même).
