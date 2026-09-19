# Architecture UI — couches DA + inventaire réel

**Propriétaire :** Netsun  
**Statut :** E2–E8 en cours d’implémentation (`Hud*` + overlay) ; SHA Phase 8 02–04 non restylées.  
**Tokens :** [TOKENS-DA.md](TOKENS-DA.md) (livrable visual-design-engineer).  
**Inventaire :** tip P10 lecture seule `853776e` — [BASELINE_AUDIT.md](BASELINE_AUDIT.md).  
**Cette branche :** docs sur `main` ; **pas** de merge Phase 10.

Objectif : *look nostalgique, expérience moderne* — pixel-art monde conservé ; chrome sombre + or. **Pas** de clone pixel-perfect. Binders sur services **existants**. Stubs folklore : extraire ou supprimer, jamais remplir en parallèle.

---

## 1. Principes

1. **`FrogGameClient` hors chrome.** Seule I/O réseau.
2. **Quatre couches orthogonales** (DA) : `GameWorldView` ⊥ `HudLayer` ⊥ `WindowLayer` ⊥ `LoginShell`.
3. **Passe tiles ≠ HUD.** `MapViewRenderer` ne peint ni barres ni chat. Invalider un overlay **ne** force **pas** `RedrawMap()`.
4. **Jetons DA** (`UiTheme`) apply-once — voir §4 et [TOKENS-DA.md](TOKENS-DA.md).
5. **Stubs folklore** (`ChatBox`, `ChatPanel`, `StatusBar`, `MiniMap`, `DialogForm`, `UIService`, `GameLoop`, `MapRenderer`, …) : remplacement **atomique** après extraction depuis `MainShellForm`, ou suppression.
6. **Live P10 à conserver :** `InventoryPanel`, `EquipmentPanel`, `CraftPanel`, `DialoguePanel`, `QuestJournalPanel`, `EnvironmentPanel`, `OptionsForm`, `HelpForm`, `TradeForm`, `UserSettings` + `ClientSettingsStore`, `InputService`, `SoundService`, `PlayerFacingMessages`.
7. **Mockup = direction.** 2 slots équipement réels ; hotbar **10 slots v1** ; onglets Guilde/Groupe **masqués** si le canal n’est pas livré sur le tip code.

---

## 2. Couches DA (orthogonales)

```text
MainShellForm  (Form unique : phases, bind FrogGameClient, *ForTest)
 │
 ├─ LoginShell            pré-jeu  ← _panelLogin + _panelCharacter
 │     LoginView / CharacterSelectView
 │
 └─ InGameShell           (après Enter)
       ├─ GameWorldView   carte / entités SEUL  ← _picMap + MapViewRenderer
       ├─ HudLayer        6 modules ancrés (overlay, pas bande qui mange la carte)
       │     Status HG · Minimap+quête HD · Chat BG · Hotbar bas centre · Menu BD
       └─ WindowLayer     overlays (Inv, Perso, Quêtes, Dialogue, Magasin, Options, Help, Trade)
```

`⊥` = pas de logique panel dans le paint carte ; pas de `Invalidate` Form depuis le chat.

| Couche DA | Rôle | Source **réelle** (`853776e`) | Ne pas faire |
| --- | --- | --- | --- |
| **GameWorldView** | Rendu monde, interpolation 16 ms | `_mapScroll` + `_picMap` + `UI/MapViewRenderer` + `_smoothTimer` | Remplir `Services/MapRenderer` / `EntityRenderer` / `GameLoop` |
| **HudLayer** | 6 modules §3 | Aujourd’hui : `_lblCombat`, `_lblPlayerStatus`, `_topChrome`, chat **inline** (`_cmbChannel` + `_txtChat` + `_txtLog`), toolbar mêlée/sort | Remplir `StatusBar` / `ChatPanel` / `MiniMap` stubs **à côté** du shell |
| **WindowLayer** | Même chrome toutes fenêtres | `InventoryPanel`, `EquipmentPanel`, `QuestJournalPanel`, `DialoguePanel`, `CraftPanel`, `EnvironmentPanel`, `OptionsForm`, `HelpForm`, `TradeForm` + shop/banque inline | Nouveau paper-doll N slots ; second store settings |
| **LoginShell** | Écran immersif compte | `_panelLogin` / `_panelCharacter` | Perdre hôte/port (les **déplacer** vers Options → Réseau + F9 éventuel) |

Binders = adapters mince sur events `FrogGameClient` + `UserSettings` / `InputService` / `SoundService` / `PlayerFacingMessages`. Pas de ViewModel qui recalcule HP/or.

---

## 3. HudLayer — 6 modules (ancrages DA, réf. 1280×720)

HUD overlay semi-transparent (ou `bg.panel.solid` si alpha coûte). Emprise max indicative ; scalable DPI.

| Module | Ancrage | Contenu | Emprise | Bind réel |
| --- | --- | --- | --- | --- |
| **Status** | HG | Portrait simple + nom + Lv + barres HP/MP | ~280×72 | `CombatStateReceived`, `_username` — **pas** `StatusBar.cs` stub |
| **Minimap** | HD | Cache local + nom zone | ~180×180 | `_map` + positions mémoire ; chrome **vide** en E2, données E3 ; nouveau fichier au bind, stub 1-ligne remplacé alors |
| **QuestTracker** | sous minimap | Titre `text.gold` + 1–2 objectifs | ~180×70 | **Même** `QuestJournalSnapshot` que `QuestJournalPanel` |
| **Chat** | BG | Onglets + log borné + input | ~360×200 | Extraire chrome inline → vrai `ChatPanel` ; `_txtLog` peut rester journal système |
| **Hotbar** | bas centre | **10 slots v1** (1–0) | ~64–120 px h | Mêlée / sort / interact = handlers actuels |
| **MenuRing** | BD | 5 pills : Perso / Inv / Quêtes / Carte / Options | ~220×56 | `Toggle` WindowLayer ; Carte = fermer overlays / focus monde |

Densité : ≤ 3 blocs par coin. Une fenêtre « focus » ; les autres atténuées. Or : inventaire **ou** HUD, pas les deux gros.

Abandonner la métaphore onglets Chat|Gameplay|Quêtes pour le **shell in-game** (E2) — les actions migrent vers hotbar / fenêtres / raccourcis, **mêmes** `Send*`.

---

## 4. Tokens + chrome (E0)

Source normative : [TOKENS-DA.md](TOKENS-DA.md) §1–3. `UiTheme` mappe ces jetons. `MapViewRenderer` **n’utilise pas** `UiTheme`.

| Jeton | Hex | Usage |
| --- | --- | --- |
| `bg.app` | `#0E1218` | Fond / letterbox derrière carte |
| `bg.panel` | `#141A24` ~90 % | HUD / fenêtres ; **fallback solid** `bg.panel.solid` `#161C28` si alpha WinForms coûte |
| `bg.panel.header` | `#1A2230` | Titlebar 28–32 px |
| `accent.gold` / `accent.gold.hi` | `#C9A227` / `#E8C547` | Filets, hover, CTA |
| `text.primary` | `#F2F4F8` | Corps (≥ 4.5:1 sur panel) |
| `text.gold` | `#E8C547` | Titres quête seulement (pas longs paragraphes) |
| `bar.hp` / `bar.mp` / `bar.xp` | `#C62828` / `#1565C0` / `#2E7D32` | Barres `CombatStateWire` |
| `chat.*` | voir TOKENS-DA | Couleurs lignes ; onglet masqué si canal absent |

**Typo :** Segoe UI / Inter / system UI uniquement sur le chrome. **Pas** de pixel-font UI (monde / logo asset seulement).

**Cadre unique** (toutes fenêtres WindowLayer + Login card) :

- Radius 6–8 (slots 4 ; MenuRing pill 999)
- Double filet or (`accent.gold.dim` + `accent.gold`)
- Titlebar 28–32 + X 20×20 (`state.error`)
- `shadow.panel` 0 4px 16px `#00000066`
- Padding 10–12 ; `gap.hud` 8

Contrôles réutilisables E0 : `DaPanel`, `DaButton`, `DaTab`, `DaSlot` (noms indicatifs) — **pas** un second toolkit WPF.

---

## 5. WindowLayer — même chrome, binders existants

| Fenêtre DA | Contrôle live | Notes |
| --- | --- | --- |
| Inventaire | `InventoryPanel` + `EquipmentPanel` + or combat | 2 slots serveur ; silhouette = chrome, pas N slots interactifs |
| Personnage | payload stats + `CombatState` | Lecture seule ; « + » seulement si points serveur existent (aujourd’hui : édition masquée) |
| Quêtes | `QuestJournalPanel` | Détail `bg.parchment` = **seule** surface claire |
| Dialogue | `DialoguePanel` | Pas `DialogForm` stub |
| Magasin | combos shop shell | + `TradeForm` modal P10 |
| Options | `OptionsForm` | Nav G : Graphisme / Son / Contrôles / Interface / **Réseau** (hôte/port/TLS **affichage** ; ne pas changer le TLS serveur). Store existant. |
| Aide | `HelpForm` | F1 conservé |
| Login | LoginShell | Compte / mdp / souvenir ; inscription / reconnect secondaires |

---

## 6. Overlay / perf (WinForms)

| Mode | Quand | Risque |
| --- | --- | --- |
| A — thème sur chrome actuel | Transition E0 seulement si besoin smoke | Faible |
| B — GameWorldView plein cadre + HudLayer opaque/solid | E2 | Moyen si Invalidate Form |
| C — TransparencyKey | Interdit v1 | Élevé |
| Alpha `bg.panel` | Option E0 | Si scintillement → **solid** `#161C28` |

**Non négociable :**

- Pas d’`Invalidate`/`Refresh` full-form à 16 ms
- Redraw **confiné** GameWorldView
- Chat ListBox **bornée**
- Double-buffer `_picMap` conservé
- HUD jamais dans `MapViewRenderer.Render`
- Panels fermés : `Visible=false` (pas de paint)
- Animations UI ≤ 150–200 ms
- DPI : layout DIP ; viser 100 / 125 / 150 % (E8)

Contrat host (E2) : `Register(anchor)`, `Show/Hide/Toggle`, `IsTextInputFocused` = `InputService.IsTextInputFocus`.

Z-order : GameWorldView < HudLayer < WindowLayer < modal (dialogue, options, help, trade, mort).

---

## 7. Chat — canaux honnêtes

| Tip code | Onglets visibles |
| --- | --- |
| `main` (3 canaux) | Général (= Global), Local (= Map), Système, Whisper |
| P10 `853776e` (enum Party + Guild) | + Groupe / Guilde **si** le combo live les envoie déjà |

Masquer l’onglet si le canal n’est **pas** livré — pas de faux READY. Cette PR docs n’ajoute aucun opcode.

---

## 8. Hôte / port / ops

Login immersif (E1) = compte + mot de passe. **Host/port** : Options → Réseau (et raccourci dev F9 éventuel).  
`HostTextBoxForTest` / `PortNumericForTest` **suivent** le champ, quel que soit l’écran — ne pas les supprimer (playtest / smokes).

---

## 9. Tests

- Phase 8 `*ForTest` : getters suivent LoginShell / Chat / panels.
- P10 : `Phase10ClientSettingsSmokeTests`, `Phase10TradePanelSmokeTests`.
- Captures DA : `artifacts/client-ui-modernization/` (E0–E2 d’abord). SHA Phase 8 : l’étape qui change le chrome possède le manifeste.

---

## 10. Risques DA (alignés client-engineer)

| Risque | Mitigation |
| --- | --- |
| Clone pixel-perfect | Tokens + layout ; art login = assets licence |
| Perte host/port | Options → Réseau + F9 ; `*ForTest` |
| Onglets Guilde/Groupe sans canal | Masquer si non livré |
| Hotbar 20 slots vides | **10 slots v1** |
| Alpha GDI | `bg.panel.solid` |
| DPI 125/150 | E8 ; DIP |
| HUD dans la passe tiles | GameWorldView découplé |
| Remplir stubs folklore | Extraire depuis `MainShellForm` |
| Wire / TLS « pour le look » | Interdit |

---

## 11. Refus

- Nouveau opcode / TLS / Hello
- Recalcul HP/MP/or
- Double boucle (`GameLoop` stub + `_smoothTimer`)
- Modifier `cursor/phase10-beta-release` depuis cette branche
- Pixel-font sur le chrome
