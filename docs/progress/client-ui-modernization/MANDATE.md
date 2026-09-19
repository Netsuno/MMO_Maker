# Mandat — modernisation UI du client FRoG

**Propriétaire :** Netsun  
**Chantier :** client UI modernization — **séparé** de Phase 10 / PR #8 (`cursor/phase10-beta-release`).  
**Branche :** `cursor/client-ui-modernization` (depuis `main`)  
**Ce run :** audit + architecture + plan d’étapes. **Pas** de réécriture HUD / overlay complète.

Ne jamais attribuer ce chantier à un autre nom dans ces docs. Propriétaire = **Netsun**.

---

## Référence visuelle

L’image de direction (mockup FRoG : HUD + fenêtres sombre/or) fournie avec le mandat est la **référence UX**, pas un clone pixel-perfect.

Objectifs de direction :

- MMORPG 2D fantasy / rétro + chrome UI **moderne sombre, accents dorés**
- Lisible, fenêtres cohérentes, résolutions modernes
- Zone de jeu maximale, HUD peu intrusive, panneaux **modulaires**
- Pixel-art du monde **conservé** ; l’UI peut être un peu plus moderne que le monde

Ne pas recopier le mockup au pixel. Ne pas inventer de paper-doll à N slots ni de fiche guilde. Canaux Party/Guild et trade : déjà sur le tip P10 `853776e` — les **conserver** au restyle, ne pas les recréer / ne pas ajouter d’opcode depuis cette branche. Inventaire factuel : [BASELINE_AUDIT.md](BASELINE_AUDIT.md).

---

## Surfaces cibles (direction, pas scope de ce run)

| # | Surface | Direction mockup | Réalité produit aujourd’hui |
| --- | --- | --- | --- |
| 1 | Écran principal | Carte presque plein écran ; HUD HG (portrait / nom / niveau / HP / MP) ; minimap HD ; tracker quête ; chat BG ; hotbar ; menu BD | `MainShellForm` (~119 Ko sur P10) : TopChrome + statut + carte `PictureBox` + onglets 360 px + log. Pas de HUD overlay / minimap / hotbar. |
| 2 | Inventaire | Overlay : équipement gauche, grille droite, stacks, or | `InventoryPanel` : `ListBox` + Équiper / Déposer. Or via `CombatStateWire`, pas dans le panneau. |
| 3 | Personnage | Portrait, classe, guilde, stats Force/Agi/…, onglets | Stats STR/AGI/DEX/INT/VIT/LUCK dans le JSON perso ; **édition UI masquée**. Pas de fiche overlay. Canal chat Guild (P10) ≠ fiche guilde. |
| 4 | Quêtes | Liste En cours / Terminées + détail ; même source que le tracker | `QuestJournalPanel` + `QuestJournalSnapshot`. Pas de tracker HUD. |
| 5 | Dialogue PNJ | Portrait, texte, choix | `DialoguePanel` dans l’onglet Quêtes. |
| 6 | Magasin | Achat / Vente, icône, prix | Combos + boutons dans l’onglet Gameplay ; paquets shop existants. |
| 7 | Options | Graphismes / Son / Commandes / Interface / Réseau | Sur P10 `853776e` : `OptionsForm` + `UserSettings` + `ClientSettingsStore` **live** (fenêtre, volume, AZERTY/QWERTY, rebind). Restyler, ne pas recréer. |
| 8 | Login | Même DA, auth existante | Phase `Login` claire (hôte / port / compte) — chrome clair WinForms. |

---

## Architecture imposée

- Panneaux **overlay** (à terme), séparation **contrôles / logique métier**.
- Chemin produit : `MainShellForm` + `FrogGameClient` (hors chrome) + panneaux live + `OptionsForm` / `HelpForm` / `TradeForm` + `UserSettings` / `InputService` / `SoundService` + `MapViewRenderer`.
- Folklore stubs (`ChatBox`, `ChatPanel`, `StatusBar`, `MiniMap`, `UIService`, `GameLoop`, …) : **ne pas** les remplir en parallèle. Extraire depuis le shell ou supprimer après découpe (ADR-0003).
- **Aucune** logique serveur / gameplay réécrite uniquement pour l’apparence.
- Ne pas recoupler un modèle VB6.

---

## Fluidité

- Rendu carte **indépendant** des panneaux UI.
- Animation / interpolation déjà côté client (`_smoothTimer` 16 ms) : à conserver.
- Cible **~60 FPS** ; pas de latence visible liée à un WinForms lourd (invalidation globale, realloc bitmap carte à chaque tick UI).

---

## Ordre obligatoire (avant code produit)

1. Analyser l’UI actuelle
2. Identifier contrôles / systèmes
3. Dire ce qui se conserve vs présentation-only
4. Proposer une architecture compatible
5. Découper en **petites étapes indépendantes**

Ces cinq points sont livrés dans ce dossier :

| Fichier | Rôle |
| --- | --- |
| [STATUS.md](STATUS.md) | État du chantier (plan only) |
| [BASELINE_AUDIT.md](BASELINE_AUDIT.md) | (1)(2)(3) — inventaire factuel |
| [ARCHITECTURE.md](ARCHITECTURE.md) | (4) — overlays, thème, services |
| [STEP_PLAN.md](STEP_PLAN.md) | (5) — étapes 1..N, done, risques 60 FPS |

---

## Hors scope (strict)

- Phase 10 / branche `cursor/phase10-beta-release` / PR #8
- Merge de cette PR
- Réécriture serveur, protocole, inventaire autoritaire, quêtes, shop, combat
- Clone pixel-perfect du mockup
- Canaux Guilde / Groupe (absents de `ChatChannel` ; social **DEFERRED** hors de ce chantier)
- Implémentation HUD complète dans **ce** run
