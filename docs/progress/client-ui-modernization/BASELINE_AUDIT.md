# Baseline audit — UI Frog.Client

**Propriétaire chantier :** Netsun  
**Branche docs :** `cursor/client-ui-modernization` (depuis `main` @ `f74b34c` — **aucun** checkout / merge Phase 10)  
**Inventaire UI faisant foi :** lecture seule tip Phase 10 `853776e0d75312458e47200cd5fa797ea96fd8f0` (`cursor/phase10-beta-release`, 2026-09-19)  
**Repo :** https://github.com/Netsuno/MMO_Maker

Ce fichier est l’état **factuel** du client joueur. Direction mockup : [MANDATE.md](MANDATE.md). Cible découpe : [ARCHITECTURE.md](ARCHITECTURE.md).

Les audits Phase 0 / Phase 9 et le premier jet de ce dossier (tip `main` seul) sont **périmés** pour Options, settings, input, aide, trade et chrome haut.

---

## 0. Deux snapshots — ne pas les mélanger

| Snapshot | SHA | Rôle pour ce chantier |
| --- | --- | --- |
| `main` (cette PR) | `f74b34cca09dda819fe26747d48ee16d27007dfd` | Base git des **docs**. Le working tree de cette branche n’embarque **pas** le code P10. |
| Phase 10 tip (lecture seule) | `853776e0d75312458e47200cd5fa797ea96fd8f0` | **Inventaire client-engineer** : shell ~119 Ko, Options/Help/Trade live, settings, chat 5 canaux dans le combo. |

Ne pas cherry-picker P10 ici. Ne pas toucher wire / opcodes / TLS. Quand une étape UI sera codée, se rebaser ou se greffer **après** accord sur le tip produit — en préservant les smokes `Phase10ClientSettings*` et les `*ForTest` Phase 8.

---

## 1. Qu’est-ce que le client réellement ?

**Pas de `GameForm`.** Le shell exécuté est `MainShellForm` (`net8.0-windows` WinForms).

Sur `853776e` : `Frog.Client/MainShellForm.cs` = **119 447 octets**, **3605 lignes**. Point d’entrée inchangé : `Program.Main` → `Application.Run(new MainShellForm(options))`.

Host de phases : **Login / Characters / Game** (`_panelLogin`, `_panelCharacter`, `_panelGame` dans `_hostPages`).

| Fichier **live** (logique à conserver ; chrome restylable) | Rôle sur `853776e` |
| --- | --- |
| `MainShellForm.cs` | Orchestration : phases, layout, bind `FrogGameClient`, input, settings |
| `Network/FrogGameClient.cs` (~1826 lignes) | TCP / paquets / events UI-thread — **hors chrome UI** |
| `Network/TcpFrameCodec.cs` | Framing live |
| `UI/MapViewRenderer.cs` | Rendu GDI+ carte → `Bitmap` (passe tiles **sans** HUD) |
| `Assets/ClientTilesetLoader.cs` | PNG tilesets |
| `Controls/InventoryPanel.cs` | Liste + Équiper / Déposer |
| `Controls/EquipmentPanel.cs` | Arme / Armure + Déséquiper |
| `Controls/CraftPanel.cs` | Recette **par nom** (Guid secours hidden) |
| `Controls/DialoguePanel.cs` | Speaker / texte / choix |
| `Controls/QuestJournalPanel.cs` | Journal + turn-in |
| `Controls/EnvironmentPanel.cs` | Carte / région / météo / éclairage |
| `Forms/OptionsForm.cs` | Modal : fenêtre, volume, AZERTY/QWERTY, rebind |
| `Forms/HelpForm.cs` | Modal aide FR (F1 / bouton Aide) |
| `Forms/TradeForm.cs` | Modal échange (snapshot + confirm/unconfirm/cancel) |
| `Config/UserSettings.cs` | POCO persisté (fenêtre, volume, preset, bindings) |
| `Config/ClientSettingsStore.cs` | JSON atomique `%LocalAppData%\Frog\client-settings.json` (override env) |
| `Config/ClientVersion.cs` | Badge `10.3.0` |
| `Services/InputService.cs` | ZQSD / WASD + flèches ; `IsTextInputFocus` |
| `Services/SoundService.cs` | Volume 0–100 / `Gain` (pas de lecture audio encore) |
| `Services/PlayerFacingMessages.cs` | Libellés FR ; jamais jeton / mot de passe |
| `ClientSmokeTestAccess.cs` | Hooks smokes + screenshots |

`FrogGameClient` n’est **pas** un contrôle. Le moderniser = hors chantier chrome (sauf si un event UI manque déjà — alors pas un nouveau opcode).

---

## 2. Folklore stubs (1 ligne `// TODO`, **non** utilisés par le shell)

Vérifiés absents de `MainShellForm` / des forms live sur `853776e` :

| Fichier | Notes |
| --- | --- |
| `Controls/ChatBox.cs` | stub |
| `Controls/ChatPanel.cs` | stub — le chat **réel** est inline dans le shell |
| `Controls/StatusBar.cs` | stub — le statut live est `_lblPlayerStatus` |
| `Controls/MiniMap.cs` | stub — minimap = étape **tardive** |
| `Controls/ClockLabel.cs` | stub |
| `Controls/FpsLabel.cs` | stub |
| `Forms/DialogForm.cs` | stub — le live est `DialoguePanel` |
| `Services/UIService.cs` | stub |
| `Services/MapRenderer.cs` | stub — le live est `UI/MapViewRenderer` |
| `Services/EntityRenderer.cs` | stub |
| `Services/GameLoop.cs` | stub — le live est `_smoothTimer` |
| `Services/AuthService.cs`, `ChatService.cs`, `InventoryService.cs` | stubs (+ `EquipmentService`, `DialogService`, `MovementService`, `CommandService`, `ClockService`) |
| `Network/NetworkService.cs`, `PacketReader.cs`, `PacketWriter.cs` | stubs |
| `Models/*` (Player, Item, …) | stubs — types live = `Frog.Core.Protocol` |

**Règle (client-engineer) :** ne **pas** remplir ces stubs en parallèle du shell. Soit **extraire** depuis `MainShellForm` vers de vrais `UserControl` / `Form` (remplacement **atomique** du fichier stub + smokes), soit **supprimer / renommer** après la découpe. Interdit : stub + implémentation partielle du même nom.

L’analyse `Frog.Client/Docs/analyse_client.md` qui parle de `GameForm` est un plan VB6 historique, pas le runtime.

---

## 3. Layout sur `853776e` (ce que le joueur voit)

`ClientUiPhase` : Login → CharacterSelect → Playing.

Chrome **global** (toutes phases) :

- `_topChrome` (`Dock.Top`) : Aide, Options, badge version, Copier diagnostics
- `_lblPlayerStatus` (`Dock.Top`) : ligne d’état FR (`PlayerFacingMessages` / `[ui]` dans le log)
- `_txtLog` (`Dock.Bottom`) : journal système + **réception chat** (TextBox multiligne — pas une ListBox)
- `_hostPages` : un des trois panneaux phase

### 3.1 Login / Characters

Même flux auth (Connecter / Login / Inscription / Reconnecter). Playtest auto-login. Stats STR…LUCK toujours `Visible = false`.

### 3.2 Game

```text
+-- _topChrome (Aide, Options, version, diagnostics) ---------------+
| _lblPlayerStatus                                                  |
| +-- toolbar jeu (map, perso, logout, cible, melee, sort, hint) -+ |
| | +-- _mapScroll + _picMap ----------+ +-- TabControl 360 px --+ | |
| | | MapViewRenderer -> Bitmap        | | Chat | Gameplay | Quetes | | |
| | +----------------------------------+ +-----------------------+ | |
| +----------------------------------------------------------------+ |
+-- _txtLog --------------------------------------------------------+
```

| Surface | Implémentation `853776e` |
| --- | --- |
| Carte | `PictureBox` `_picMap` + `MapViewRenderer` ; `EnableDoubleBuffer` déjà posé |
| Chat **inline** | Onglet Chat : `_cmbChannel` (Global / Map / Whisper / **Party** / **Guild**) + `_txtWhisperTo` + `_txtChat` + Envoyer. Historique = `_txtLog`. Le stub `ChatPanel` n’est **pas** ça. Inventaire engineer « ListBox+Combo inline » = chrome **dans le shell** (Combo réel ; historique aujourd’hui TextBox — la cible d’extraction est ListBox **bornée** + Combo). |
| Statut joueur | `_lblPlayerStatus` + `_lblCombat` (Niv/XP/HP/MP/Or dans l’onglet Gameplay) + respawn |
| Options / Aide | Modales `OptionsForm` / `HelpForm` (F1). **Pas** des stubs. |
| Inventaire / équip / banque / sol / shop | Onglet Gameplay (panels live + combos / ListBox banque-sol) |
| Craft / quête / dialogue / environnement | Onglet Quêtes (`SidePanels` à extraire) |
| Trade | `TradeForm` modal, `TradeSnapshotWire` |

---

## 4. Surfaces — fonctionne / conserve / présentation-only

Légende : **Fonctionne** (tip `853776e`) · **Se conserve** (logique / tests) · **Présentation-only**.

### 4.1 Top chrome + HUD

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Ligne statut FR | Oui — `_lblPlayerStatus` | `PlayerFacingMessages`, `ShowPlayerStatus*ForTest`, redaction diagnostics | Extraire **TopChrome** |
| HP/MP/XP/or/mort | Oui — `CombatStateWire` → `_lblCombat` | `Combat*ForTest` | Barres overlay ; pas de recalcul client |
| Aide / Options / version | Oui | `HelpForm`, `OptionsForm`, `ClientVersion`, `Phase10ClientSettingsSmokeTests` | Skin sombre/or des **mêmes** modales |
| Interpolation | Oui — timer 16 ms | `_smoothTimer`, `InputService` | Ne pas peindre le HUD dans la passe tiles |

### 4.2 Carte / viewport

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Carte 32 px + joueurs | Oui | `MapViewRenderer` (monde seulement) | Extraire **GameViewport** autour de `_picMap` |
| Mini-carte | **Non** (stub) | Aucun paquet dédié | Étape **tardive** (UI-12) ; cache local |
| Double-buffer map | Oui — `EnableDoubleBuffer` | Garder | Redraw **confiné viewport** ; pas `Invalidate` full-form |

**Risque perf :** `RedrawMap()` alloue encore un `Bitmap` carte entière à chaque tick sale. UI-15. Toute étape qui `Refresh()` le `Form` aggrave.

### 4.3 Chat

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Envoi 5 canaux (P10) | Oui — combo + `SendChatAsync` | Enum `ChatChannel` P10 (Global…Guild). Sur `main` : 3 canaux seulement. **Ne pas ajouter d’opcode** ici. | Extraire un **vrai** `ChatPanel` (ListBox bornée + Combo + saisie) depuis le shell ; **remplacer** le stub, ne pas le remplir à côté |
| Réception | Oui → `_txtLog` | `ChatMessageReceived`, `LogContainsForTest` | Historique dédié borné |
| Slash modération | Oui | `ModerateWire` | Inchangé |
| Focus saisie | Oui — `InputService.IsTextInputFocus` bloque le move | Conserver | OverlayHost réutilise le même prédicat |

### 4.4 Inventaire / équip / banque / sol / shop / trade

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Inv / équip | Oui | Panels + `Arme: {Name}` + `*ForTest` | **HudInventoryDock** (tabs restylés) |
| Banque / sol / shop | Oui | Listes / combos / `Phase7` smokes | Même dock ou fenêtres liées |
| Trade | Oui (P10) | `TradeForm` + `Phase10TradePanelSmokeTests` | Modal restylée ; pas de nouveau protocole trade |
| 2 slots équipement | Oui | Weapon / Armor | Pas de paper-doll N slots |

### 4.5 Quêtes / dialogue / craft / environnement

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Journal + turn-in | Oui | `QuestJournalPanel`, smokes Phase 8 | **SidePanels** + tracker plus tard (même snapshot) |
| Dialogue | Oui | `DialoguePanel` + token | Overlay / modal restyle |
| Craft nommé | Oui (P10 combo) | `CraftPanel` | Skin ; pas de nouveau craft serveur |
| Environnement | Oui | `EnvironmentPanel` | Compact HUD optionnel |

### 4.6 Login / settings / input / son

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Login / persos | Oui | Auth, jeton jamais logué, `*ForTest` | Extraire **LoginView** / **CharacterSelectView** |
| `UserSettings` + store | Oui (P10) | JSON atomique, env path, `SettingsForTest` | **Ne pas** réécrire un second store. Restyler `OptionsForm` |
| `InputService` | Oui | Presets + rebind + flèches | Raccourcis fenêtres (I/J/…) en plus, locaux |
| `SoundService` | Volume persisté | `Gain` / mute | Slider déjà là ; pas de mixer tant que pas de lecture |
| Fiche Force/Guilde mockup | Stats JSON cachées | 6 clés STR…LUCK | Lecture seule ; guilde chat ≠ fiche guilde |

---

## 5. Contrats tests

**Phase 8 (hooks `*ForTest`)** — conserver en déplaçant les getters avec les contrôles :

- `GameplayClientSmokeTests`
- `Phase8GameplayClientSmokeTests` / `Phase8ClientPanelRenderingTests`

**Phase 10 (à préserver dès que le code UI se greffe sur un tip ≥ `853776e`)** :

- `Phase10ClientSettingsSmokeTests` (store JSON, AZERTY/QWERTY, volume, F1, badge `10.3.0`, diagnostics sans secret, statut `[ui]`)
- `Phase10TradePanelSmokeTests`

Règles :

1. Ne pas supprimer `*ForTest` ; les faire suivre LoginView / ChatPanel / TopChrome.
2. Chaînes assertées (`Arme: `, `Achat: Achat reussi.`, `Aide`, `Options`, `ZQSD`, …) : même PR si on les change.
3. SHA screenshots Phase 8 : l’étape thème possède le manifeste.
4. Linux = compile only ; WinForms = `windows-latest`.

---

## 6. Couleurs / DA

Toujours clair (`245,248,252` / `SystemColors.Control`) + `_lblPlayerStatus` `235,240,246`. Carte fallback inchangée (`60,90,60`). Pas de `UiTheme`. Monde **sans** jetons or.

---

## 7. Synthèse écart mockup ↔ `853776e`

| Mockup | Produit P10 | Action UI |
| --- | --- | --- |
| Carte plein écran + HUD | Split + tabs 360 px + log + top chrome | GameViewport + overlays |
| Barres HP/MP HG | Texte `_lblCombat` + `_lblPlayerStatus` | TopChrome / PlayerHud |
| Minimap | Stub | **Tardif** |
| Chat 5 onglets overlay | Combo 5 canaux inline + log | Vrai `ChatPanel` extrait |
| Hotbar | Toolbar | Bind actions existantes |
| Inventaire grille | ListBox + 2 slots | HudInventoryDock |
| Options | **Modal live** + JSON | Restyle, pas recréer |
| Trade | Modal live | Restyle |
| Login DA | Formulaire clair | LoginView skin |

**Aucun nouveau opcode / TLS / wire** pour cet écart.
