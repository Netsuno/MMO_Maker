# Plan d’étapes — DA E0–E8 + inventaire réel

**Propriétaire :** Netsun  
**Ordre DA (validé) :** **E0 → E2 avant** tout peaufinage fenêtre par fenêtre. Puis E3…E8.  
**Tokens / chrome :** [TOKENS-DA.md](TOKENS-DA.md). Architecture : [ARCHITECTURE.md](ARCHITECTURE.md).  
**Inventaire :** `MainShellForm` réel (`853776e`) — [BASELINE_AUDIT.md](BASELINE_AUDIT.md).  
**Base code :** `main` @ `e58a185` (Phase 10 mergée). Pas de wire / opcodes / TLS. Mouvement = priorité #1.

Si une étape glisse vers le serveur ou `cursor/phase10-beta-release` : **stop**.

---

## Vue d’ordre

```text
E0  Tokens + chrome réutilisable (panel, bouton, onglet, slot)
 └─► E1  LoginShell immersif (compte ; host/port → Options→Réseau)
      └─► E2  GameWorldView plein cadre + HudLayer 6 modules VIDES
            ├─► E3  Status + Minimap + QuestTracker branchés
            ├─► E4  Chat (onglets honnêtes, ListBox bornée)
            ├─► E5  Hotbar 10 slots + MenuRing
            ├─► E6  WindowLayer (Inv / Perso / Quêtes / Dialogue / Magasin / Trade)
            ├─► E7  OptionsForm restyle (UserSettings déjà live)
            └─► E8  DPI 100/125/150 + guides + revue captures
Perf viewport (ex-UI-15)  ← contrainte dès E2, mesurée en E8
```

**Règle DA :** ne pas fignoler Inventaire/Quêtes/Dialogue tant que E0–E2 n’ont pas le même chrome et une carte plein cadre.

Correspondance ancienne numérotation UI-1…UI-15 (docs précédentes) :

| Ancien | DA | Note |
| --- | --- | --- |
| UI-1 | **E0** | Jetons d’abord, pas seulement skin des onglets actuels |
| UI-10 | **E1** | Login **avant** les fenêtres jeu |
| UI-2 + viewport | **E2** | HUD vides + carte plein cadre |
| UI-3 + UI-7 tracker + UI-12 | **E3** | Minimap n’est plus « tout à la fin » : chrome E2, données E3 |
| UI-4 | **E4** | |
| UI-13 | **E5** | |
| UI-5…UI-9, UI-8, UI-14 | **E6** | Fenêtres **après** le shell |
| UI-11 | **E7** | Store déjà live — restyle + nav Réseau |
| (nouveau) | **E8** | DPI + guides |
| UI-15 | contrainte E2–E8 | |

---

## Règles communes

- Extraire depuis `MainShellForm` ; **ne pas** remplir `ChatPanel` / `StatusBar` / `MiniMap` / `DialogForm` stubs en parallèle.
- Smokes Phase 8 `*ForTest` ; dès tip ≥ `853776e` : `Phase10ClientSettingsSmokeTests` + `Phase10TradePanelSmokeTests`.
- Perf : pas d’Invalidate full-form ; redraw **GameWorldView** only ; chat borné ; double-buffer map ; HUD hors passe tiles ; alpha → `bg.panel.solid` `#161C28` si besoin.
- Canaux Guilde/Groupe : onglet **masqué** si le tip n’envoie pas le canal. P10 `853776e` les a dans le combo — les garder alors. Pas de nouvel opcode.
- Hotbar **10 slots v1**.
- Done = code + smokes + captures DA si E0–E2 + ligne [STATUS.md](STATUS.md).

---

## E0 — Tokens + chrome de base

**But :** contrôles réutilisables (`DaPanel` / bouton or / onglet / slot) = jetons [TOKENS-DA.md](TOKENS-DA.md). Pas encore de refonte layout.

**Fait :**

- `UiTheme` : `bg.app` `#0E1218`, `bg.panel` `#141A24` (~90 %) + fallback solid `#161C28`, or `#C9A227` / hi `#E8C547`, texte `#F2F4F8`, HP/MP/XP `#C62828` / `#1565C0` / `#2E7D32`
- Typo Segoe/Inter/system — **pas** de pixel-font chrome
- Radius 6–8, double filet or, titlebar 28–32
- Capture : 1 panel + 1 bouton or sur fond sombre
- Apply-once, hors timer 16 ms

**Hors :** extraire tous les panels ; MiniMap données ; clone planche.

---

## E1 — LoginShell

**But :** écran immersif compte / mdp / souvenir / Connexion. Inscription + reconnect **secondaires**.

**Fait :**

- Extraire `_panelLogin` (+ `_panelCharacter` peut rester page 2 du même shell)
- **Hôte/port** quittent le panneau joueur → Options → Réseau (E7 peut poser la nav ; E1 doit déjà **déplacer** les champs ou un lien F9). `HostTextBoxForTest` / `PortNumericForTest` suivent.
- Playtest auto-login + jeton jamais logué
- Asset logo **licence OK** — pas de copie d’art commercial
- `LoginButtonForTest` / `PassTextBoxForTest` / diagnostics

**Hors :** launcher ; peaufiner Inventaire.

---

## E2 — Carte plein cadre + HUD vides

**But :** `GameWorldView` occupe le cadre ; `HudLayer` pose les **6** blocs stylés **vides** (Status, Minimap, QuestTracker, Chat, Hotbar, MenuRing). Onglets Chat|Gameplay|Quêtes **ne** structurent plus le shell (peuvent rester hidden pour smokes le temps du remap `*ForTest`).

**Fait :**

- Extraire viewport (`_picMap` + `MapViewRenderer`) — sibling, pas TransparencyKey
- Emprises §4 TOKENS-DA ; `gap.hud` 8
- 60 FPS **carte seule** (pas de paint HUD dans les tuiles)
- Host : `IsTextInputFocused` = `InputService.IsTextInputFocus`
- Modules vides = chrome E0 seulement (pas de bind données sauf ce qui casse un smoke — alors bind minimal)

**Hors :** fignoler chaque fenêtre (E6) ; remplir stub `MiniMap` avec un Render 60 Hz.

**Perf :** critère DA « 60 FPS carte seule ». Invalidate viewport only.

---

## E3 — Status + Minimap + QuestTracker (données)

**But :** brancher les 3 blocs HD/HG.

**Fait :**

- Status : `CombatStateWire` → barres HP/MP (+ XP optionnelle) ; nom / niveau ; respawn `IsDead`
- Minimap : cache au `MapDataReceived` **seulement** ; point joueur invalidate local ; remplacer le stub `MiniMap.cs` **à ce moment**
- QuestTracker : 1–2 lignes, `text.gold`, **même** snapshot que `QuestJournalPanel`
- Lisibilité / contraste barres (revue DA)

**Hors :** fog, N slots équipement.

---

## E4 — Chat

**But :** extraire le chat **inline** vers un vrai `ChatPanel` (ListBox bornée + onglets + saisie). Remplacement atomique du stub.

**Fait :**

- Couleurs `chat.*` TOKENS-DA
- Onglets : Général / Local / Système / Whisper ; Guilde / Groupe **seulement si** canal livré
- Cap historique ; slash modération ; `ChatTextBoxForTest` / `SelectChatChannelForTest`
- `_txtLog` = système / `[ui]` (`LogContainsForTest`)

**Hors :** `ChatBox` stub en plus ; emotes.

---

## E5 — Hotbar + MenuRing

**But :** 10 slots (1–0) + 5 pills BD → `Toggle` WindowLayer.

**Fait :**

- Slots = mêlée / sort sélectionné / interact (`InputService`) — **mêmes** `Send*`
- Menu : Perso / Inventaire / Quêtes / Carte / Options (`OpenOptions` existant)
- Aide (F1) reste `HelpForm` (bouton chrome ou menu)

**Hors :** 2e rangée ; nouveaux cooldowns serveur.

---

## E6 — Fenêtres (même chrome E0)

**But :** restyler / extraire les panels **live** : inventaire, perso, quêtes, dialogue, magasin, `TradeForm`, banque/sol, craft/environnement.

**Fait :**

- Chrome identique (double filet, titlebar, X)
- Inventaire : 2 slots réels ; grille visuelle ok ; `Arme:` / `*ForTest`
- Quêtes : liste + parchemin (`bg.parchment` seule exception claire)
- Dialogue : `DialoguePanel` (pas `DialogForm`)
- Shop + `Phase10TradePanelSmokeTests`
- Une fenêtre focus ; Esc ferme

**Hors :** drag-drop obligatoire ; paper-doll interactif fantôme.

---

## E7 — Options

**But :** restyler `OptionsForm` (déjà live P10). Nav : Graphisme / Son / Contrôles / Interface / **Réseau**.

**Fait :**

- Un seul `ClientSettingsStore` — **ne pas** recréer
- Réseau : hôte, port (champs déplacés depuis E1) ; TLS = libellé / état, **pas** un nouveau handshake ici
- Volume / AZERTY-QWERTY / rebind / fenêtre / plein écran
- `Phase10ClientSettingsSmokeTests` PASS

**Hors :** vsync GPU réel ; second JSON.

---

## E8 — DPI + guides

**But :** 100 / 125 / 150 % Windows ; focus clavier or (`state.focus`) ; captures + quickstart chrome.

**Fait :**

- Layout DIP ; hitboxes ; cadres non flous
- Revue DA sur captures **réelles** E0–E7 (alignement, contraste or/texte, emprise HUD)
- Guides « chaque bouton » seulement **après** E5–E6 (sinon docs mortes)

**Hors :** chiffre FPS inventé — mesurer ou se taire.

---

## Perf (contrainte continue, ex-UI-15)

| Risque | Mitigation |
| --- | --- |
| Full-map `Bitmap` / tick | Surface persistante / clip viewport |
| Alpha panels GDI | `bg.panel.solid` |
| Invalidate Form | GameWorldView sibling |
| Chat rebuild 60 Hz | ListBox cap, event-only |
| HUD dans les tuiles | Refuser la PR |

---

## Hors plan

| Sujet | Pourquoi |
| --- | --- |
| Coder Phase 10 / PR #8 | Séparé |
| Wire / TLS | Interdit pour le look |
| Clone pixel planche | DA §8 |
| Stubs folklore en parallèle | Client-engineer |
| Peaufiner E6 avant E2 | Ordre DA |
| Merge cette PR docs | Pas ce run |

---

## Succès

E0–E2 : tokens + login + carte plein cadre + 6 HUD vides, 60 FPS monde.  
E3–E8 : données + fenêtres + options + DPI, smokes Phase 8/10 verts, mécaniques inchangées.

Succès **de ce run** : E2 overlay + E3–E5 wires + E7 nav Options + notes DPI. SHA 02–04 intactes. [STATUS.md](STATUS.md).
