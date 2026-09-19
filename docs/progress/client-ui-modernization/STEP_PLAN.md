# Plan d’étapes — Client UI modernization

**Propriétaire :** Netsun  
**Ordre recommandé :** UI-1 → UI-15. Étapes **indépendamment mergeable** si done + smokes.  
**Ce run :** documentation seulement.  
**Inventaire :** tip P10 lecture seule `853776e` — [BASELINE_AUDIT.md](BASELINE_AUDIT.md).  
**Branche :** séparée de `cursor/phase10-beta-release`. Pas de merge P10 ici. Pas de wire / opcodes / TLS.

Si une étape glisse vers le serveur ou Phase 10 : **stop**.

---

## Vue d’ordre

```text
UI-1  Thème (chrome P10 actuel : TopChrome + tabs + log — layout inchangé)
  └─► UI-2  OverlayHost (ancres, focus = InputService.IsTextInputFocus)
        ├─► UI-3  TopChrome / PlayerHud (_lblPlayerStatus + CombatState)
        ├─► UI-4  Extraire ChatPanel (ListBox bornée + Combo) depuis le shell
        ├─► UI-5  HudInventoryDock (inv / équip / banque / sol / shop)
        ├─► UI-6  Dialogue (SidePanels / modal)
        ├─► UI-7  Quêtes + tracker (même snapshot)
        ├─► UI-8  Boutique chrome + TradeForm restyle (modales existantes)
        ├─► UI-9  Fiche personnage (lecture seule)
        ├─► UI-10 LoginView + CharacterSelectView
        ├─► UI-11 Restyle OptionsForm / HelpForm (store déjà live)
        ├─► UI-12 MiniMap — TARDIF (pas le stub)
        ├─► UI-13 Hotbar + menu BD
        └─► UI-14 Banque / sol (si pas déjà dans UI-5)
UI-15 Isolation viewport / budget 60 FPS   ← parallèle dès UI-1
```

Découpe **extract-from-shell**, jamais « remplir le stub en parallèle ».

---

## Règles communes

- Branche ≠ `cursor/phase10-beta-release`.
- **Pas** de nouveau `PacketId`, pas de change TLS / Hello / bind.
- Smokes :
  - Phase 8 : `*ForTest` conservés (déplacés avec le contrôle).
  - Dès greffe sur tip ≥ `853776e` : `Phase10ClientSettingsSmokeTests` + `Phase10TradePanelSmokeTests` verts.
- Perf : pas d’`Invalidate`/`Refresh` full-form à 16 ms ; redraw **viewport only** ; chat borné ; double-buffer map ; HUD hors passe tiles. UI-15 **réduit** le coût `RedrawMap`.
- Stub folklore : extraire **puis** remplacer/supprimer le fichier 1-ligne. Pas les deux à la fois.
- Done = code + smokes + ligne dans [STATUS.md](STATUS.md).

---

## UI-1 — Thème sur le chrome **actuel** (P10)

**But :** sombre + or sur `_topChrome`, `_lblPlayerStatus`, Login/Character, toolbar, onglets, `OptionsForm`/`HelpForm` **sans** extraire.

**Fait :** `UiTheme` apply-once ; contrastes ; hiérarchie `BuildLayout` inchangée ; smokes Phase 8 (+ Phase 10 settings si le tip code est P10).

**Hors :** OverlayHost, MiniMap, nouveaux UserControls.

**Perf :** `Apply` hors `SmoothTimer_OnTick`.

---

## UI-2 — OverlayHost

**But :** ancres + Show/Hide ; tabs peuvent rester.

**Fait :** host sibling de `GameViewport` (même si le viewport n’est pas encore extrait : sibling de `_mapScroll`) ; **pas** `TransparencyKey` ; `IsTextInputFocused` = `InputService.IsTextInputFocus` ; carte redessinée seulement sur le chemin mouvement.

**Hors :** migrer tous les onglets d’un coup.

**Perf :** interdit `Invalidate` sur `MainShellForm` à 16 ms.

---

## UI-3 — TopChrome / PlayerHud

**But :** extraire `_topChrome` + `_lblPlayerStatus` ; barres HP/MP depuis `CombatStateReceived` (pas depuis `_lblCombat` recalculé).

**Fait :** Aide / Options / version / diagnostics **mêmes** handlers (`OpenHelp`, `OpenOptions`, `CopyDiagnostics*ForTest`) ; respawn sur `IsDead` ; `PlayerFacingMessages` inchangé.

**Hors :** portrait obligatoire, fake regen, remplir `StatusBar.cs` stub.

**Perf :** update event-driven.

---

## UI-4 — Extraire `ChatPanel` (vrai contrôle)

**But :** sortir le chat **inline** du shell vers un UserControl : Combo canaux + saisie + **ListBox bornée**. Remplacer **atomiquement** le stub `Controls/ChatPanel.cs`.

**Fait :**

- Canaux = ceux du tip code (3 sur `main`, 5 sur P10). Pas de nouvel opcode.
- Historique cap (ex. 200) — append, pas clear/rebuild 60 Hz.
- `SendChatButtonForTest` / `ChatTextBoxForTest` / `SelectChatChannelForTest` / `LogContainsForTest` (log système peut rester `_txtLog`).
- Focus saisie : déjà bloqué par `InputService` — ne pas régresser.
- Slash modération inchangé.

**Hors :** emotes ; remplir `ChatBox.cs` en plus.

**Perf :** ListBox bornée (exigence client-engineer).

---

## UI-5 — HudInventoryDock

**But :** extraire onglet Gameplay : `InventoryPanel`, `EquipmentPanel`, banque, sol, shop → dock restylé (tabs ou fenêtre).

**Fait :** mêmes events Equip/Drop/Unequip ; or = `CombatState.Gold` ; textes `Arme:` / smokes ; 2 slots seulement.

**Hors :** drag-drop ; paper-doll N slots ; remplir `InventoryService` stub.

**Perf :** `ApplySnapshot` event-only ; pas de `RedrawMap`.

---

## UI-6 — Dialogue (SidePanels)

**But :** `DialoguePanel` en modal/overlay ; auto-show `DialogueStatePush`.

**Fait :** `DialoguePanelForTest` / choix / token. **Pas** `DialogForm` stub.

**Perf :** event-only.

---

## UI-7 — Quêtes + tracker

**But :** `QuestJournalPanel` fenêtre + tracker (même `ApplySnapshot`).

**Fait :** turn-in ; `SelectPhase8TabForTest` remappé si l’onglet disparaît ; filtre En cours / Terminées local.

**Hors :** minimap quests (UI-12).

---

## UI-8 — Boutique chrome + TradeForm

**But :** restyler shop (mêmes `SendShop*` / `TrySelect*ForTest`) et **`TradeForm` existant** (P10) — pas une nouvelle UI trade.

**Fait :** `Phase10TradePanelSmokeTests` (révision affichée, confirm) verts ; GUID secours hidden.

**Hors :** nouveau protocole d’échange.

---

## UI-9 — Fiche personnage

**But :** overlay lecture seule (nom, combat, 6 stats JSON).

**Fait :** pas de `CharacterStatsUpdate` ; pas de fiche guilde (le canal Guild P10 ≠ une guilde joueur).

---

## UI-10 — LoginView + CharacterSelectView

**But :** extraire `_panelLogin` / `_panelCharacter`. Même auth, playtest, jeton jamais logué.

**Fait :** tous les `LoginButtonForTest` / `PassTextBoxForTest` / etc. suivent la vue.

**Hors :** launcher.

---

## UI-11 — Restyle Options + Help (déjà live)

**But :** **ne pas** recréer `UserSettings` / `ClientSettingsStore`. Skin sombre/or de `OptionsForm` et `HelpForm`.

**Fait :** fenêtre, plein écran, volume, preset, rebind, écriture atomique — comportement P10 ; `Phase10ClientSettingsSmokeTests` PASS ; F1 / `HelpButtonForTest` / `OptionsButtonForTest`.

**Hors :** vsync GPU ; second fichier de config ; remplir un stub (il n’y en a plus pour Options).

**Perf :** apply settings une fois (resize), pas par frame.

---

## UI-12 — MiniMap (**tardif**)

**But :** miniature cache de la carte mémoire + point joueur.

**Fait :** rebuild cache **seulement** au `MapDataReceived` ; invalidate local du point ; **nouveau** fichier — supprimer le stub `MiniMap.cs` au moment de l’ajout, pas avant.

**Hors :** fog, ping serveur.

**Perf :** **interdit** d’appeler `MapViewRenderer.Render` à 16 ms pour la mini.

---

## UI-13 — Hotbar + menu BD

**But :** 1–0 = mêlée / sort / interact (`InputService`) ; menu Perso / Inventaire / Quêtes / Options = `Toggle` extraits. « Carte » = fermer overlays.

**Hors :** nouveaux cooldowns serveur.

---

## UI-14 — Banque / sol (si UI-5 ne les a pas pris)

**But :** chrome cohérent, mêmes `*ForTest` listes / pickup.

---

## UI-15 — Viewport / 60 FPS (parallèle dès UI-1)

**But :** monde fluide avec overlays. Mesurer, puis réduire `RedrawMap`.

**Fait (min) :**

- Redraw **confiné** `GameViewport` ; pas full-form.
- Surface persistante / clip viewport si possible (plus de `new Bitmap(full map)` par tick).
- HUD hors passe tiles.
- Chat borné.
- Double-buffer map conservé.
- Avant/après documentés (même machine) — pas de chiffre inventé.

**Hors :** MonoGame, GPU, UDP.

| Risque | Preuve (`853776e`) | Mitigation |
| --- | --- | --- |
| `new Bitmap(w,h)` full-map / tick | `MapViewRenderer.Render` + `RedrawMap` | surface persistante / dirty |
| `PictureBox.Image` reassign 60 Hz | `RedrawMap` | blit in-place |
| Invalidate Form | WinForms | sibling viewport |
| Chat / inv rebuild 60 Hz | à éviter | event + cap ListBox |
| HUD dans les tuiles | refuser PR | `MapViewRenderer` monde-only |

Cible : ~60 FPS ressenti, carte Phase 7, HUD + 1 fenêtre. Saisie chat / toggle I sans latence chrome.

---

## Hors plan

| Sujet | Pourquoi |
| --- | --- |
| Coder dans `cursor/phase10-beta-release` | Chantier séparé |
| Wire / opcodes / TLS | Interdit pour l’apparence |
| Remplir stubs folklore en parallèle | Règle extract-or-delete |
| MiniMap tôt | Tardif (UI-12) |
| Clone pixel mockup | Mandat |
| Merge de cette PR docs | Pas ce run |

---

## Succès chantier (plusieurs runs)

Carte dominante, chrome sombre/or, **toutes** les actions live P10 encore là (settings, aide, trade, shop, banque, quêtes, dialogue, craft, chat du tip, mêlée). `FrogGameClient` hors chrome. Protocole / TLS inchangés.

Succès **de ce run** : docs à jour vs inventaire `853776e`. [STATUS.md](STATUS.md).
