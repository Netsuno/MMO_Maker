# FRoG Client — Tokens DA + risques UX

> Copie du livrable visual-design-engineer, intégrée dans [ARCHITECTURE.md](ARCHITECTURE.md) et [STEP_PLAN.md](STEP_PLAN.md).  
> **Inventaire réel** (client-engineer, tip P10 `853776e`) : [BASELINE_AUDIT.md](BASELINE_AUDIT.md) — shell = `MainShellForm`, **pas** `GameForm`. Les noms `ChatPanel` / `StatusBar` / `MiniMap` au §0 sont des **cibles DA**, pas des contrôles live (stubs folklore). Extraire depuis le shell, ne pas remplir les stubs en parallèle.

**Demande :** modernisation UI client (réf. visuelle `frog-client-ui-reference.png`).  
**Rôle DA :** direction visuelle / tokens / densité / cohérence — **pas** de push/commit.  
**Référence propriétaire :** Netsun.

**Principe produit (réf.) :** *look nostalgique, expérience moderne* — pixel-art monde **conservé** ; chrome UI moderne sombre + or. **Pas** de clone pixel-perfect de la planche.

---

## 0. Constat UI actuelle (baseline)

| Aspect | Aujourd’hui (MainShell / playtest) | Cible |
| --- | --- | --- |
| Look | WinForms clair, boutons système, « outil dev » | MMORPG 2D fantasy, chrome sombre |
| Layout jeu | Viewport carte en coin / onglets Chat·Gameplay·Quêtes | Carte **plein cadre** ; HUD en coins |
| Login | Champs Hôte/Port/Compte + boutons Connecter/Login… | Écran immersif + panneau compte (ops : host/port en Options/avancé) |
| Panneaux | Contrôles existants (`ChatPanel`, `InventoryPanel`, `QuestJournalPanel`, `MiniMap`, `StatusBar`…) | Même **mécaniques / services** ; nouvelle **présentation** |

Garder : état serveur, `UserSettings` / Options, services inventaire·équipement·chat·dialogues, 60 FPS, rendu carte **découplé** des overlays lourds, zéro couplage VB6.

---

## 1. Tokens couleur

Valeurs **cibles** (hex approximatifs pour implémentation ; ajustables ±5 % après 1ère capture réelle).

### Surfaces
| Token | Hex | Usage |
| --- | --- | --- |
| `bg.app` | `#0E1218` | Fond derrière carte (letterbox) |
| `bg.panel` | `#141A24E6` (~90 % opac.) | Panneaux HUD / fenêtres |
| `bg.panel.solid` | `#161C28` | Quand alpha impossible / perf |
| `bg.panel.header` | `#1A2230` | Barre titre fenêtre |
| `bg.input` | `#0A0E14` | Champs texte |
| `bg.row.hover` | `#1E2A3C` | Ligne liste survol |
| `bg.row.selected` | `#24344A` | Ligne / onglet actif |
| `bg.parchment` | `#E8D9B8` | Détail quête (exception claire) |
| `bg.slot` | `#0C1018` | Case inventaire / hotbar |

### Or & chrome
| Token | Hex | Usage |
| --- | --- | --- |
| `accent.gold` | `#C9A227` | Bordures, titres secondaires, CTA |
| `accent.gold.hi` | `#E8C547` | Hover / focus |
| `accent.gold.dim` | `#8A7018` | Bordure inactive |
| `border.panel` | `#C9A22799` | Contour simple |
| `border.panel.double` | or dim + or | Double filet (fenêtres) |

### Texte
| Token | Hex | Usage |
| --- | --- | --- |
| `text.primary` | `#F2F4F8` | Corps |
| `text.secondary` | `#A8B0C0` | Labels secondaires |
| `text.muted` | `#6B7385` | Désactivé / hint |
| `text.gold` | `#E8C547` | Titres quête, noms importants |
| `text.on.parchment` | `#2A2218` | Sur fond parchemin |
| `text.danger` | `#E85D5D` | Erreurs |

### Gameplay (barres / chat)
| Token | Hex | Usage |
| --- | --- | --- |
| `bar.hp` | `#C62828` | Vie |
| `bar.mp` | `#1565C0` | Mana |
| `bar.xp` | `#2E7D32` | XP (si affichée) |
| `chat.general` | `#F2F4F8` | |
| `chat.local` | `#90CAF9` | |
| `chat.guild` | `#CE93D8` | *si canal livré* |
| `chat.party` | `#A5D6A7` | *si canal livré* |
| `chat.system` | `#FFCC80` | |
| `chat.whisper` | `#F48FB1` | |

### Feedback
| Token | Hex | Usage |
| --- | --- | --- |
| `state.focus` | `#E8C547` | Anneau focus clavier |
| `state.error` | `#B71C1C` | Bouton fermer / erreur |
| `state.ok` | `#2E7D32` | Succès discret |

**Contraste :** texte primary sur `bg.panel` ≥ 4.5:1 ; or sur sombre pour titres seulement (pas corps long).

---

## 2. Typographie

| Token | Police | Taille (ref 1280×720) | Poids | Usage |
| --- | --- | --- | --- | --- |
| `font.ui` | Segoe UI / Inter / system UI | — | — | Famille UI |
| `type.hud.name` | `font.ui` | 13–14 px | SemiBold | Nom joueur |
| `type.hud.meta` | `font.ui` | 11–12 px | Regular | Lv, labels barres |
| `type.chat` | `font.ui` | 12 px | Regular | Chat (line-height 1.35) |
| `type.window.title` | `font.ui` | 14 px | SemiBold | Titre fenêtre |
| `type.body` | `font.ui` | 12–13 px | Regular | Listes, options |
| `type.quest.title` | `font.ui` | 13 px | SemiBold | Titre quête (`text.gold`) |
| `type.login.logo` | display (logo asset) | — | — | Logo FRoG (asset, pas faux-or CSS excessif) |
| `type.npc.nameplate` | `font.ui` | 11–12 px | SemiBold | Au-dessus sprites |

**Règle :** une seule famille UI ; pas de pixel-font sur le chrome (réservée au monde / logo si asset).

---

## 3. Cadres, rayons, ombres

| Token | Valeur | Usage |
| --- | --- | --- |
| `radius.panel` | 6–8 px | Fenêtres / HUD blocks |
| `radius.slot` | 4 px | Cases |
| `radius.button.pill` | 999 / cercle | Menu BD (Perso/Inv/…) |
| `border.width` | 1 px | Standard |
| `border.width.accent` | 2 px | Focus / onglet actif |
| `shadow.panel` | 0 4px 16px `#00000066` | Fenêtres overlay |
| `padding.panel` | 10–12 px | Intérieur fenêtre |
| `gap.hud` | 8 px | Entre blocs HUD coin |
| `titlebar.height` | 28–32 px | + bouton X 20×20 (`state.error` fond) |

**Cadre type fenêtre :** double filet or discret + header sombre + X rouge carré (comme réf.) — **même chrome** pour Inventaire, Perso, Quêtes, Dialogue, Magasin, Options, Login card.

---

## 4. Layout HUD (densité & ancrages)

Référence résolution design : **1280×720** (scalable). Zone de jeu = tout le client moins letterbox ; HUD en **overlay** semi-transparent, pas de bande WinForms qui mange la carte.

| Zone | Ancrage | Contenu | Emprise max (approx.) |
| --- | --- | --- | --- |
| État joueur | HG | Portrait circulaire + nom + Lv + barres HP/MP | ~280×72 px |
| Minimap | HD | Carte + nom zone | ~180×180 px |
| Suivi quête | sous minimap | Titre or + 1–2 objectifs | ~180×70 px |
| Chat | BG | Onglets + log + input | ~360×200 px (repliable plus bas) |
| Hotbar | bas centre | 1–2 rangées × 10 slots | hauteur ~64–120 px |
| Menu | BD | 5 boutons ronds : Perso / Inv / Quêtes / Carte / Options | ~220×56 px |

**Densité :**
- HUD permanent = **bas** : ne pas empiler plus de 3 blocs par coin.
- Une seule fenêtre overlay « focus » en avant ; les autres peuvent rester ouvertes mais **atténuées** (option).
- Ne pas dupliquer l’info (ex. or : inventaire **ou** HUD, pas les deux gros).

**Ancrages vs WinForms actuel :** abandonner la métaphore onglets Chat|Gameplay|Quêtes pour le **shell in-game** ; les actions Gameplay (mêlée, banque…) migrent vers hotbar / fenêtres / raccourcis — **même appels réseau**.

---

## 5. Fenêtres overlay — cohérence

Toutes partagent : chrome §3, tabs style or, listes `bg.row.*`.

| Fenêtre | Structure DA | Notes |
| --- | --- | --- |
| Inventaire | Gauche équipement (silhouette + slots) · droite grille + onglets Équipement/Objets · or en pied | Grille ~4×5 visible ; scroll si plus |
| Personnage | Portrait + classe · onglets Stats / Détails / Réputation · « + » points | Ne montrer « + » que si points dispo (état serveur) |
| Quêtes | Liste G · détail parchemin D · abandon | Parchemin = seule surface claire |
| Dialogue PNJ | Portrait · texte · choix avec flèche or | Largeur confortable, pas plein écran |
| Magasin | Onglets Achat/Vente · lignes icône\|nom\|prix | Pas de DSN / secrets |
| Options | Nav G (Graphisme, Son, Contrôles, Interface, Réseau) · contenu D | **Host/Port/TLS** ici ou sous-réseau (pas polluer login joueur) |
| Login | Fond art · logo · compte/mdp · souvenir · Connexion | Inscription / reconnect jeton = secondaires |

---

## 6. Architecture compatible (avis DA → client-engineer)

Découpage recommandé **sans** casser les services :

1. **`GameWorldView`** — rendu carte / entités seul (thread ou contrôle léger) ; **aucune** logique panel lourde.
2. **`HudLayer`** — 6 modules ancrés (Status, Minimap, QuestTracker, Chat, Hotbar, MenuRing).
3. **`WindowLayer`** — overlays modulaires (Inventaire, …) ouverts via MenuRing / raccourcis.
4. **`LoginShell`** — écran pré-jeu séparé du shell in-game.
5. **Binding** — ViewModels / adapters sur services existants (inventaire, équipement, chat, dialogues, `UserSettings`).

Perf : panels non visibles = `Visible=false` ou unload visuel ; pas de invalidate carte sur tick chat ; animations UI ≤ 150–200 ms, pas de full-screen redraw.

---

## 7. Découpage en étapes (revue DA du plan)

| Étape | Contenu | Critère DA |
| --- | --- | --- |
| **E0** | Tokens + chrome de base (panel, bouton, onglet, slot) en contrôles réutilisables | Capture : 1 panel + 1 bouton or sur fond sombre |
| **E1** | Login immersif (champs compte ; host/port → Options) | Aligné réf. sans clone art copyrighté — **asset licence OK** |
| **E2** | Shell in-game : carte plein cadre + 6 blocs HUD vides stylés | Emprises §4 ; 60 FPS carte seule |
| **E3** | Status + Minimap + QuestTracker branchés données réelles | Lisibilité barres / contraste |
| **E4** | Chat onglets + couleurs canaux (canaux absents = onglet masqué, pas faux READY) | |
| **E5** | Hotbar + MenuRing → ouverture fenêtres | |
| **E6** | Inventaire / Perso / Quêtes / Dialogue / Magasin (restyle panels existants) | Même chrome |
| **E7** | Options (migration UserSettings + Réseau) | |
| **E8** | Polish DPI Windows, focus clavier, captures guides | Revue DA sur captures réelles |

**Ordre validé DA :** E0→E2 avant de peaufiner chaque fenêtre (sinon incohérence chrome).

---

## 8. Risques UX / DA

| Risque | Impact | Mitigation |
| --- | --- | --- |
| **Clone pixel-perfect** de la planche | Dette art + possible look « générique IA » | Tokens + layout ; art login/logo = assets licence maison |
| **Perte host/port** pour testeurs | Ops/bêta bloqués | Section Options → Réseau ; raccourci dev `F9` éventuel |
| **Canaux chat Guilde/Groupe** absents Phase 10 | Faux READY | Onglets seulement si protocole/canal vivant ; sinon Général/Local/Système (+ Whisper) |
| **Hotbar 20 slots** vs peu de sorts | Vide / confusion | 10 slots v1 ; 2e rangée si besoin |
| **Alpha panels** sur WinForms GDI | Perf / scintillement | `bg.panel.solid` fallback ; composer bitmap une fois |
| **DPI / scaling Windows** | Cadres flous, hitboxes | Layout en DIP ; tester 100/125/150 % |
| **Parchemin quête** vs thème sombre | Rupture visuelle OK si **seule** exception | Ne pas multiplier les surfaces claires |
| **Portrait / icônes HD** vs pixel world | Dissonance | Portraits stylisés simples ; icônes items cohérentes échelle |
| **Trop de fenêtres ouvertes** | Masque la carte | Une « focus » ; raccourci fermer ; translucidité |
| **Bouton X rouge** accessibilité | Contraste / Daltonisme | Toujours tooltip + raccourci Échap |
| **Couplage logique UI** dans paint carte | Drops FPS | Strict sépar. WorldView / HudLayer |
| **Guides « chaque bouton »** | Docs obsolètes | Après E5–E6, maj `UI-CLIENT-MainShell` + quickstart |

---

## 9. Hors scope DA (rappel)

- Pas de merge / push / branche produit depuis visual-design-engineer.
- Pas de copie d’UI d’un jeu commercial protégé (la planche est une **direction**, pas une texture à découper).
- Mécaniques et autorité serveur inchangées.

---

## 10. Livrables suivants (quand captures E0–E2)

Revue DA sur screenshots réels : alignement, contraste or/texte, emprise HUD, cohérence chrome fenêtres.
