# STATUS — Client UI DA v2 step 4 (menu 5 icônes)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Menu ring BD : 5 boutons ronds icône+label (Perso, Inventaire, Quêtes, Carte, Options) |
| **Propriétaire** | Netsun |
| **Statut** | Step 4 only — layout planche, contraste #16 conservé |
| **Base** | `main` @ `deadcac` (merge PR #20 status portrait) |
| **Branche** | `cursor/client-ui-da-v2-menu-five` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/21 vers `main` — **pas de merge** |
| **Tip** | `b979917` |

Protocole / gameplay / réseau / mouvement / skin / chrome fenêtres / portrait status / login : **non touchés**.
Présentation + layout menu BD seulement. Contraste #16 inchangé : cercle sombre `bg.slot` + icônes/labels crème ; or = filet seulement.

---

## Avant → après

| Surface | Avant (pills texte) | Après (tokens DA) |
| --- | --- | --- |
| `HudMenuRing` | 5 pills horizontales AutoSize (`Perso` / `Inv` / …), bord rectangulaire | **5 cercles** ø**40** (plage 36–48) + label crème sous l’icône : **Perso, Inventaire, Quêtes, Carte, Options** |
| Chrome bouton | `StyleContrastHudButton` rect (fill `bg.slot` `#0C1018` + filet or) | Même tokens : fill `bg.slot` `#0C1018` ; filet 1 px `accent.gold` `#C9A227` (hover `accent.gold.hi`) ; icône + label `text.primary` `#F2F4F8` |
| Kenney `menu/btn_round` | Déjà retiré au step 1 | **Toujours absent** (owner-draw `FillEllipse` / `DrawEllipse`) |
| Carte | Ferme l’overlay (`SetWindowLayerVisible(false)`) | **Inchangé** (déjà branché) |
| Options | `OpenOptions()` | **Inchangé** (déjà branché) |
| Emprise | ~300×44 | Rangée BD ~320×60 (`gap` 4 px entre cercles) |

`UiTheme.Apply` repose `StyleContrastHudButton` + `BorderSize = 0` sur `HudMenuRing.RoundButton` pour ne pas ramener un filet rectangulaire. Les cellules (FlowLayout) restent transparentes.

---

## Hors scope (PR suivantes)

- Step 5 : login immersif
- Step 6 : remplacement frames Kenney
- Split contenu Inventaire ≠ Perso (même onglet Gameplay aujourd’hui)

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
