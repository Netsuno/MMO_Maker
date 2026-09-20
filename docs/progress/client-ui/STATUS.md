# STATUS — Client UI DA v2 step 1 (contrast)

Skin joueur v2 (32×32 Eldiran, nearest ×1) : voir [STATUS-player-skin-v2.md](STATUS-player-skin-v2.md).

| Champ | Valeur |
| --- | --- |
| **Chantier** | Fermer or-sur-or sur hotbar + menu ring (planche DA v2) |
| **Propriétaire** | Netsun |
| **Statut** | Step 1 only — slots / pills dark fill + gold border + cream icons |
| **Base** | `main` @ `614bdb9` (merge PR #15 player skin) |
| **Branche** | `cursor/client-ui-da-v2-contrast` |
| **PR** | Draft vers `main` — **pas de merge** |

Protocole / gameplay / réseau inchangés. Présentation + layout HUD seulement.
Kenney `bars/*` HP/MP, chrome fenêtres, portrait status, login : **non touchés** (steps 2–6).

---

## Avant → après

| Surface | Avant (Kenney + tint or) | Après (tokens DA) |
| --- | --- | --- |
| `HudHotbar` slots | Case Kenney brune + icône/chiffre or (`CreateGoldTintAttributes`) | Fill `bg.slot` `#0C1018` ; bordure seule `accent.gold` `#C9A227` ; icônes/chiffres `text.primary` `#F2F4F8` |
| Slots 4–10 | Même chrome, texte muted | Même fill sombre ; bordure `accent.gold.dim` ; chiffre `text.muted` |
| `HudMenuRing` | Pill Kenney brune + icône or | Cercle/chip sombre `bg.slot` + filet or ; icône + label crème. **5 boutons déjà branchés** conservés (Perso / Inv / Quêtes / Carte / Options) — pas d’expansion step 4 |

`UiTheme.Apply` repose `StyleContrastHudButton` sur ces contrôles pour ne pas les ramener à `BgPanelHeader`.

---

## Hors scope (PR suivantes)

- Step 2 : chrome fenêtres (titlebar / X rouge / tabs) — [STATUS-da-v2-chrome.md](STATUS-da-v2-chrome.md)
- Step 3 : portrait status HG — [STATUS-da-v2-status-portrait.md](STATUS-da-v2-status-portrait.md)
- Step 4 : layout 5 icônes menu (les 5 fils existent déjà)
- Step 5 : login immersif
- Step 6 : remplacement frames Kenney

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
