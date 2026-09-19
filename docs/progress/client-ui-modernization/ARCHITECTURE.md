# Architecture UI — overlays compatibles (Frog.Client)

**Propriétaire :** Netsun  
**Statut :** proposition (aucune de ces extractions n’est dans ce run).  
**Inventaire source :** tip P10 lecture seule `853776e` — [BASELINE_AUDIT.md](BASELINE_AUDIT.md).  
**Cette branche :** docs sur `main` ; **pas** de merge Phase 10.

Objectif : moderniser la **présentation** sans recoupler le TCP, sans réécrire le gameplay, sans toucher wire / opcodes / TLS, sans remplir les stubs folklore.

---

## 1. Principes

1. **`FrogGameClient` reste hors chrome.** Seule I/O réseau. Les vues n’ouvrent pas de socket.
2. **Source de vérité = snapshots / events déjà là** (combat, inv, quêtes, dialogue, banque, sol, environnement, chat, catalogue, trade P10).
3. **Contrôle = présentation + intentions.** Le shell (puis un facade optionnel) appelle `Send*Async`.
4. **Passe tiles ≠ passe HUD.** `MapViewRenderer` ne dessine pas barres / chat / chrome. Invalider un overlay **ne** force **pas** `RedrawMap()`.
5. **Thème = jetons**, apply à la construction.
6. **Stubs folklore :** ne pas les implémenter « à côté ». Extraire depuis `MainShellForm` **ou** supprimer/renommer après découpe. Remplacement atomique si on réutilise un nom (`ChatPanel`).
7. **Settings / input / son / messages joueur déjà live (P10) :** `UserSettings`, `ClientSettingsStore`, `InputService`, `SoundService`, `PlayerFacingMessages` — **conserver**, restyler l’UI qui les expose.
8. **ADR-0003 / ADR-0004 :** pas de parité VB6 ; pas de WPF sur le client joueur.
9. **Mockup = direction.** Paper-doll N slots et fiche guilde restent hors données.

---

## 2. Mapping proposé (client-engineer)

Noms cibles = extraits depuis le shell, **pas** de nouveaux services métier.

```text
MainShellForm  (reste le Form : phases, bind FrogGameClient, *ForTest)
 │
 ├─ TopChrome              ← _topChrome + _lblPlayerStatus (+ plus tard barres CombatState)
 ├─ LoginView              ← _panelLogin
 ├─ CharacterSelectView    ← _panelCharacter
 ├─ GameViewport           ← _mapScroll + _picMap + MapViewRenderer (monde only)
 ├─ ChatPanel              ← chrome chat INLINE (combo + saisie + historique borné)
 ├─ HudInventoryDock       ← tabs Gameplay : InventoryPanel, EquipmentPanel, banque, sol, shop
 ├─ SidePanels             ← CraftPanel, QuestJournalPanel, DialoguePanel, EnvironmentPanel
 ├─ OptionsForm            ← garder modal, restyler
 ├─ HelpForm               ← garder modal, restyler
 └─ TradeForm              ← garder modal, restyler
 MiniMap                   ← étape tardive (nouveau contrôle ; pas le stub 1-ligne)
```

`FrogGameClient` : **aucun** de ces extraits.

```text
┌─ Presentation ────────────────────────────────────────────────────┐
│  MainShellForm     host Login | Characters | Game                   │
│  UiTheme           jetons sombre/or                                 │
│  OverlayHost       ancres / z-order (après extract dock)            │
│  Views ci-dessus   UserControls extraits du shell                   │
└───────────────────────────────┬───────────────────────────────────┘
                                │ Apply*(snapshot) / intentions
┌─ Session déjà live ───────────────────────────────────────────────┐
│  InputService, SoundService, PlayerFacingMessages                   │
│  UserSettings + ClientSettingsStore                                 │
│  MainShellForm bind (extract facade seulement si BuildLayout explose)│
└───────────────────────────────┬───────────────────────────────────┘
                                │ Send* / events
┌─ Network (hors chrome) ───────────────────────────────────────────┐
│  FrogGameClient + TcpFrameCodec + Frog.Core.Protocol                │
└───────────────────────────────────────────────────────────────────┘
┌─ World render ────────────────────────────────────────────────────┐
│  MapViewRenderer → Bitmap viewport ; _smoothTimer ; tilesets        │
│  Double-buffer déjà sur _picMap — garder                            │
└───────────────────────────────────────────────────────────────────┘
```

---

## 3. Overlay host (après les extraits dock)

WinForms : pas de calque transparent cheap.

| Mode | Quand | Risque 60 FPS |
| --- | --- | --- |
| **A — Dock + thème** | UI-1 (layout actuel P10 : top chrome + split + log) | Faible |
| **B — Overlay opaque ancré** | UI-2+ une fois GameViewport / ChatPanel extraits | Moyen si `Invalidate` Form |
| **C — TransparencyKey** | Interdit v1 | Élevé |

**Décision :** A puis B. Fenêtres opaques (bordure or). Carte = sibling (`GameViewport`), pas enfant invalidé par le chrome.

Contrat `IOverlayHost` (UI-2) :

```text
Register(anchor, control)
Show / Hide / Toggle
IsTextInputFocused     // déléguer à InputService.IsTextInputFocus
SetMapExclusiveInput
```

Z-order : GameViewport < TopChrome / Chat / docks < fenêtres < modal (dialogue, options, help, trade, mort).

**Perf (non négociable) :**

- Pas d’`Invalidate` / `Refresh` **full-form** dans le timer 16 ms.
- Redraw **confiné** au viewport (`GameViewport` / `PictureBox`).
- Chat : `ListBox` (ou équivalent) **bornée** (cap, pas rebuild 60 Hz).
- Double-buffer map **conservé**.
- HUD **jamais** dans `MapViewRenderer.Render` (passe tiles).

---

## 4. Thème sombre + or

`Frog.Client/UI/UiTheme.cs` (UI-1) — apply récursif **une fois**.

Jetons : `BackgroundDeep`, `PanelFill`, `PanelHeader`, `BorderGold`, `TextPrimary` / `TextMuted`, `HpFill` / `MpFill` / `XpFill`, `AccentGold`, couleurs chat par canal (y compris Party/Guild **si** le tip code a déjà l’enum — pas d’ajout wire ici).

`MapViewRenderer` **n’utilise pas** `UiTheme`.

---

## 5. Binding extraits → logique existante

| Extrait | Réutilise | Source | Intention |
| --- | --- | --- | --- |
| TopChrome | `_lblPlayerStatus`, boutons Aide/Options, version, diagnostics, plus `_lblCombat` | `PlayerFacingMessages`, `CombatStateReceived` | `OpenHelp` / `OpenOptions` / respawn |
| LoginView / CharacterSelectView | champs et boutons actuels | auth / character list | mêmes `Send*` |
| GameViewport | `_picMap`, `MapViewRenderer` | map + positions | aucun paquet nouveau |
| ChatPanel (**nouveau vrai** contrôle) | `_cmbChannel`, `_txtChat`, historique | `ChatMessageReceived` ; slash modération | `SendChatAsync` |
| HudInventoryDock | `InventoryPanel`, `EquipmentPanel`, banque, sol, shop | snapshots + catalogue | Equip/Drop/Shop/Bank/Pickup |
| SidePanels | Craft / Quest / Dialogue / Environment | snapshots Phase 8 | Choice / Turn-in / Craft |
| OptionsForm / HelpForm | **garder types** | `UserSettings` / store | `ApplySettingsFromStore` |
| TradeForm | **garder type** | `TradeSnapshotWire` | Confirm/Unconfirm/Cancel existants |
| MiniMap (tardif) | nouveau, pas le stub | `_map` + positions mémoire | — |
| Hotbar (plus tard) | boutons mêlée/sort/interact | `InputService` | mêmes `Send*` |

`FrogGameClient` : colonne « hors UI chrome » — on n’y met pas de `BackColor`.

---

## 6. Ce qu’on n’invente pas

| Tentation | Conduite |
| --- | --- |
| Remplir `ChatPanel.cs` stub pendant que le shell garde le chat inline | Extraire **puis** remplacer le fichier stub |
| Second `UserSettings` / autre JSON | Un seul `ClientSettingsStore` |
| Nouvel opcode Party/Guild « pour le mockup » | Sur P10 l’enum **existe déjà** ; cette PR docs ne touche pas le wire. Sur `main` : 3 canaux jusqu’au merge P10. |
| Paper-doll 8+ slots | 2 slots live |
| Peindre HP sur les tuiles | HUD hors `MapViewRenderer` |
| TLS / version protocole / bind serveur | Hors chantier |
| WPF / MonoGame | Hors plan |

---

## 7. Input

Déjà (P10) : `InputService` + `IsTextInputFocus` → pas de move pendant le chat / mot de passe.

Cible overlay : `OverlayHost.IsTextInputFocused` **=** ce prédicat. Enter envoie le chat. Raccourcis I/J/C/O/Esc = locaux. Interact / move = `InputService` uniquement.

---

## 8. Tests

- Phase 8 : getters `*ForTest` suivent les extraits (même type de panneau).
- Phase 10 : `Phase10ClientSettingsSmokeTests` (F1, Options, badge, diagnostics, store) et `Phase10TradePanelSmokeTests` restent verts après restyle.
- Nouveau dossier captures chrome : `artifacts/client-ui-modernization/` — ne pas écraser les SHA Phase 8 tant que l’étape ne le revendique pas.

Extract `ClientSessionFacade` : seulement si `BuildLayout` devient ingérable. Pas un prérequis UI-1.

---

## 9. Refus

- Nouveau opcode / champ wire / changement TLS « pour le HUD »
- Recalcul HP/MP/or
- Deuxième TCP
- Double boucle (`GameLoop` stub + `_smoothTimer`)
- Modifier `cursor/phase10-beta-release` depuis cette branche
- Remplir folklore stubs en parallèle
