# STATUS — Client UI DA v2 step 5 (login immersif)

| Champ | Valeur |
| --- | --- |
| **Chantier** | LoginShell immersif : carte centrée, compte/mdp/souvenir/Connexion ; hôte/port hors carte joueur |
| **Propriétaire** | Netsun |
| **Statut** | Step 5 only — layout planche, tokens DA inchangés |
| **Base** | `main` @ `ee25ace` (merge PR #22 editor spawn ; includes #21 menu five) |
| **Branche** | `cursor/client-ui-da-v2-login` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/23 vers `main` — **pas de merge** |
| **Tip** | `050c422` |

Protocole / gameplay / réseau / mouvement / skin / chrome fenêtres / portrait status / menu 5 icônes : **non touchés**.
Présentation + layout login (et carte perso page 2) seulement. Contraste #16 inchangé : fill sombre `bg.slot` + texte crème ; or = filet / bordure seulement.

---

## Avant → après

| Surface | Avant (barre outil) | Après (tokens DA) |
| --- | --- | --- |
| `_panelLogin` | Rangée Hôte/Port/Compte/Mdp + 5 boutons | **`LoginShell`** plein cadre `bg.app` `#0E1218` ; carte centrée **400** px, padding **12** |
| Carte | — | `bg.panel` `#161C28` + **double filet or** (`PaintDoubleGoldFrame`) |
| Logo | Titre « Connexion » | Emblème ø**48** fill `bg.slot` `#0C1018` + filet `accent.gold` `#C9A227` ; wordmark **FRoG** crème `text.primary` `#F2F4F8` (pas d’art commercial) |
| Champs joueur | Hôte + port + compte + mdp en ligne | **Compte** + **mot de passe** empilés (280 px, `bg.input`) ; **Souvenir** (username only) |
| CTA | « Login » au milieu des boutons | **Connexion** primaire : fill `bg.slot` + filet or ; **Inscription** / **Reconnecter** secondaires |
| Connecter / Déconnecter | Même rangée que Login | Rangée serveur secondaire (même tokens) — le TCP reste 2 étapes pour les smokes |
| Hôte / port | Sur la carte joueur | **Hors carte** ; Options → Réseau (déjà là) + **F9** ops strip ; `HostTextBoxForTest` / `PortNumericForTest` suivent |
| Page perso | Stack 560 px | Même carte DA (`LoginShell.HostCenteredCard`) |

`UiTheme.Apply` reconnaît `LoginShell` / `LoginCard` pour ne pas ramener le fond `bg.panel` ni l’or-sur-or (wordmark reste crème).

---

## Hors scope (PR suivantes)

- Step 6 : remplacement frames Kenney
- Split contenu Inventaire ≠ Perso (même onglet Gameplay aujourd’hui)
- Combiner Connecter+Connexion en un seul clic (changerait le contrat smoke Connect → Login)

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
