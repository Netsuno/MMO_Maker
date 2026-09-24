# STATUS — Client UI status HG (planche portrait)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Status HG : portrait circulaire placeholder + nom + Lv + HP/MP (~280×72) |
| **Propriétaire** | Netsun |
| **Statut** | Planche jouable : cercle tête/corps existant, barres Kenney lisibles |
| **Base** | `main` @ `3dbf63f` |
| **Branche** | `cursor/client-status-hud-planche-1243` |
| **PR** | Draft [#51](https://github.com/Netsuno/MMO_Maker/pull/51) vers `main` — **pas de merge** |
| **Protocole** | **v11** inchangé |

Présentation du statut HG seulement. Pas de nouvelle planche de skin, pas de génération paperdoll, pas de bump protocole.
Panneaux sociaux et panneau équipement : mécaniques inchangées (le portrait relit l’overlay local déjà calculé).

---

## Avant → après

| Surface | Avant | Après (tokens DA) |
| --- | --- | --- |
| Status HG | Nom + `Lv N` + barres, portrait = initiale | Panneau `bg.panel` `#161C28` ; portrait circulaire **ø44** (plage 40–48) à gauche ; nom `text.primary` `#F2F4F8` + Lv + HP/MP |
| Emprise | 280×72 | **280×72** (`gap.hud` interne 8) |
| Portrait | Initiale ou buste GDI | Fill `bg.slot` `#0C1018` + filet or 1 px `accent.gold` `#C9A227` ; région circulaire ; composite **tête/corps** sud idle déjà en jeu (nearest), overlays locaux (arme / armure / casque) si équipés ; buste GDI si le composite manque |
| Barres | Kenney `bars/*` sans chiffre | **Kenney conservé** (couleurs natives + fallback `bar.hp` / `bar.mp`) + valeur `courant/max` en crème sur la piste |
| XP | Pas de barre (max absent du fil) | **Inchangé** |
| Mort | Ligne sous les barres | Libellé « Mort » sur la ligne du nom (les barres restent dans les 72 px) ; bouton Respawn inchangé |

`HudWindowChrome` / `HudHotbar` / `HudMenuRing` / login : non réécrits ici.

---

## Hors scope

- Art de skin ambre, génération de sheets paperdoll
- Bump protocole, champs fil inventés
- Éditeur, rattrapage docs Exemple
- Remplacement des frames Kenney (step 6)

Linux : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.

Smoke Login-phase : ne pas utiliser `Control.Visible` / `PointToScreen` (ancêtres `_panelGame` masqués). Le split HG est colonne 0 portrait / colonne 1 nom+barres (`GetColumn(_body) == 1`, pas la table elle-même).
