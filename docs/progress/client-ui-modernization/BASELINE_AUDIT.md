# Baseline audit — UI Frog.Client

**Audited tip :** `f74b34cca09dda819fe26747d48ee16d27007dfd` (`main`, merge PR #7)  
**Branche d’audit :** `cursor/client-ui-modernization`  
**Date :** 2026-09-19  
**Repo :** https://github.com/Netsuno/MMO_Maker  
**Propriétaire chantier :** Netsun

Ce fichier est l’état **factuel** du client joueur. Il ne décrit pas le mockup (voir [MANDATE.md](MANDATE.md)). Il ne propose pas l’architecture cible (voir [ARCHITECTURE.md](ARCHITECTURE.md)).

Le `docs/BASELINE_AUDIT.md` racine (Phase 0, 2026-08-22) et l’audit Phase 9 sont **d’autres** snapshots. Ne pas les traiter comme l’UI actuelle.

---

## 1. Qu’est-ce que le client réellement ?

`Frog.Client` est un WinExe `net8.0-windows` (WinForms). Point d’entrée : `Program.Main` → `Application.Run(new MainShellForm(options))`.

| Fichier live | Lignes (approx.) | Rôle |
| --- | --- | --- |
| `MainShellForm.cs` | 3177 | **Tout** le chrome : phases Login / CharacterSelect / Playing, layout, input, binding événements |
| `Network/FrogGameClient.cs` | 1720 | TCP, paquets, events UI-thread (`SynchronizationContext`) |
| `Network/TcpFrameCodec.cs` | — | Framing live (pas le stub `NetworkService`) |
| `UI/MapViewRenderer.cs` | 275 | Rendu GDI+ **carte entière** → `Bitmap` |
| `Assets/ClientTilesetLoader.cs` | — | PNG tilesets |
| `Controls/InventoryPanel.cs` | — | Liste + Équiper / Déposer |
| `Controls/EquipmentPanel.cs` | — | Arme / Armure + Déséquiper |
| `Controls/QuestJournalPanel.cs` | — | Journal + turn-in |
| `Controls/DialoguePanel.cs` | — | Speaker / texte / choix |
| `Controls/CraftPanel.cs` | — | Guid recette + Craft |
| `Controls/EnvironmentPanel.cs` | — | Carte / région / météo / éclairage |
| `ClientSmokeTestAccess.cs` | — | Hooks smokes + screenshots |
| `Phase7ClientContentSeed.cs` | — | Guids démo (shop, etc.) |

**Il n’existe pas de `GameForm`.** L’analyse historique `Frog.Client/Docs/analyse_client.md` nomme `GameForm` / `StatusBar` / `ChatPanel` comme plan de conversion VB6 — ce n’est **pas** le code exécuté.

`FrogGameClient` poste tous les events sur le contexte UI. `MainShellForm` s’abonne et met à jour contrôles + `AppendLog`.

---

## 2. Stubs historiques (présentation future possible, **pas** le chemin actuel)

Un seul ligne `// TODO: Implémenter …` — **aucun** usage par `MainShellForm` / `FrogGameClient`. ADR-0003 : ne pas les relancer comme parité FRoG/VB6.

| Fichier | Mentionné dans le mandat ? | État |
| --- | --- | --- |
| `Controls/StatusBar.cs` | oui | stub |
| `Controls/ChatPanel.cs` | oui | stub |
| `Controls/ChatBox.cs` | oui | stub |
| `Controls/MiniMap.cs` | oui | stub |
| `Controls/FpsLabel.cs` | — | stub |
| `Controls/ClockLabel.cs` | — | stub |
| `Forms/OptionsForm.cs` | oui | stub |
| `Forms/DialogForm.cs` | — | stub (le live est `DialoguePanel`) |
| `Config/UserSettings.cs` | oui (direction) | stub |
| `Services/UIService.cs` | oui | stub |
| `Services/AuthService.cs`, `ChatService`, `InventoryService`, `EquipmentService`, `DialogService`, `InputService`, `MovementService`, `GameLoop`, `MapRenderer`, `EntityRenderer`, `CommandService`, `SoundService`, `ClockService` | — | stubs |
| `Network/NetworkService.cs`, `PacketReader.cs`, `PacketWriter.cs` | — | stubs (live = `FrogGameClient` + `TcpFrameCodec`) |
| `Models/Player.cs`, `Item.cs`, `InventorySlot.cs`, `Equipment*.cs`, `ChatMessage.cs`, `DialogLine.cs`, `Npc.cs`, `CombatEffect.cs`, `WarpAttribute.cs` | — | stubs (live = types `Frog.Core.Protocol`) |

**Règle pour les étapes UI :** implémenter de **nouveaux** contrôles overlay (ou étendre les panneaux **live**) plutôt que de remplir ces squelettes « pour faire exister le nom ». Si un nom de fichier est réutilisé, le remplacer **en entier** et mettre à jour les smokes — ne pas mixer stub + partiel.

---

## 3. Layout et phases (ce que le joueur voit)

`ClientUiPhase` : `Login` → `CharacterSelect` → `Playing`. Un seul `Form` (1040×720, min 980×640), fond `SystemColors.Control` / login `245,248,252` (clair).

### 3.1 Login

Champs : hôte, port, compte, mot de passe. Boutons : Connecter, Déconnecter, Login, Inscription, Reconnecter (jeton). Statut jeton. Auth **existante** (`SendLoginAsync` / register / reconnect). Playtest : login automatique `__frog_playtest__` + jeton env (jamais logué).

### 3.2 Sélection de personnage

Liste persos, Entrer dans le jeu, création (nom + classe catalogue). Rangée stats STR…LUCK **`Visible = false`** (le serveur refuse `CharacterStatsUpdateRequest` hors playtest / fallback mémoire).

### 3.3 Playing — chrome actuel (pas overlay)

```text
┌ toolbar (map, changer perso, logout, cible, mêlée, sort, respawn, aide E) ─┐
│ ┌ map scroll + PictureBox (Fill) ───┐┌ TabControl 360 px ───────────────┐ │
│ │ MapViewRenderer → Bitmap entière  ││ Chat | Gameplay | Quêtes         │ │
│ └───────────────────────────────────┘└──────────────────────────────────┘ │
└ _txtLog Dock.Bottom (journal système + chat reçu) ─────────────────────────┘
```

| Onglet | Contenu |
| --- | --- |
| **Chat** | Combo canal Global / Map / Whisper, cible whisper, `_txtChat` (saisie), Envoyer. **Réception** → `AppendLog` (`[G]`/`[M]`/`[W]`), **pas** dans `_txtChat`. |
| **Gameplay** | `_lblCombat` (Niv/XP/HP/MP/Or) ; `EquipmentPanel` ; `InventoryPanel` ; shop (combos + qty + Acheter/Vendre) ; banque (list + or) ; sol + Ramasser |
| **Quêtes** | `EnvironmentPanel` ; `QuestJournalPanel` ; `DialoguePanel` ; `CraftPanel` |

Pas de portrait, pas de barres HP/MP sur la carte, pas de minimap, pas de tracker quête, pas de hotbar 1–0, pas de menu Perso/Inventaire/Quêtes/Carte/Options.

---

## 4. Surfaces demandées — fonctionne / conserve / présentation-only

Légende :

- **Fonctionne** : branché, smokes ou paquets le prouvent
- **Se conserve** : ne pas réécrire la logique / le contrat test
- **Présentation-only** : chrome, disposition, thème, contrôles visuels

### 4.1 HUD / état joueur

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| HP/MP/XP/niveau/or/mort | Oui — `CombatStateWire` → `_lblCombat` + `_btnRespawn` | Event + champs wire + `CombatHpForTest` / `CombatGoldForTest` | Déplacer vers barres overlay HG ; ne pas recalculer HP côté client |
| Portrait / classe / nom HUD | Non (nom = `_username` session) | Username + catalogue classes à la **création** | Chrome portrait ; classe HUD seulement si déjà dans payload / liste — **ne pas inventer un paquet** |
| Interpolation mouvement | Oui — timer 16 ms, prédiction + réconciliation | `_smoothTimer`, `AdvanceMovementSmoothing`, `PositionSync` 52 ms | Ne pas lier le tick UI overlay au `RedrawMap` |

### 4.2 Carte / monde

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Carte tuiles 32 px + joueurs | Oui — `MapViewRenderer` + `PictureBox` | `FrogGameClient` map / fingerprint / warps | Cadre, scroll vs caméra centrée |
| Mini-carte | **Non** (`MiniMap.cs` stub) | Aucune donnée serveur dédiée | Downscale / cache local de la carte déjà reçue |
| FPS overlay | Non (`FpsLabel` stub) | Mesure locale | Label debug |

**Risque perf actuel (déjà là) :** `RedrawMap()` alloue un `Bitmap` **carte complète** à chaque tick sale (~16 ms). Toute UI qui invalide la carte ou appelle `Refresh()` global aggrave ça. Voir [STEP_PLAN.md](STEP_PLAN.md) UI-15.

### 4.3 Chat

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Envoi Global / Map / Whisper | Oui | `ChatChannel` (3 valeurs) + `SendChatAsync` + slash modération Phase 9 | Dock BG, onglets visuels, couleurs par canal |
| Réception | Oui mais dans `_txtLog` | `ChatMessageReceived` | Historique dédié (le log mélange système + chat) |
| Général / Local / Guilde / Groupe / Système (mockup) | **Non** | Local ≈ Map ; Système ≈ `AppendLog` erreurs | **Guilde / Groupe : pas de canal wire** — ne pas les inventer |

### 4.4 Inventaire / équipement

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Liste slots + qty + nom catalogue | Oui — `InventorySnapshotWire` | `ApplySnapshot`, `ItemNameLookup`, `EquipRequested` / `DropRequested` | Grille, icônes, stacks visuels, overlay |
| Équiper / déposer | Oui — smokes `GameplayClientSmokeTests` | Texte `Arme: {Name}` (jamais GUID) ; `*ForTest` | Boutons / drag-drop **plus tard** (drag = présentation + mêmes events) |
| Arme / armure | Oui — 2 slots serveur | `EquipmentSlotKind` Weapon/Armor | Paper-doll mockup (casque, bottes…) = **slots absents** — chrome vide ou masqué, pas de nouveaux slots |
| Or inventaire | Or sur `CombatStateWire` + banque | Ne pas inventer un champ or inventaire | Afficher l’or combat sur la fenêtre inventaire |

### 4.5 Personnage / stats

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Stats STR/AGI/DEX/INT/VIT/LUCK | Reçues (`OnCharacterPayload`) ; UI édition **cachée** | JSON `stats` ; pas de réactivation édition hors playtest | Fiche lecture seule ; labels FR (Force…) **uniquement** comme alias d’affichage des 6 clés existantes |
| Guilde / réputation | Non | — | Hors chantier (pas de données) |

### 4.6 Quêtes

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Journal + objectifs + turn-in | Oui — `QuestJournalSnapshot` | `QuestJournalPanel.ApplySnapshot` + `TurnInRequested` ; smokes Phase 8 + SHA captures | Fenêtre overlay + onglets En cours / Terminées (filtre **local** sur `CharacterQuestStatus`) |
| Tracker HUD | Non | **Même** snapshot — pas de 2e source | Liste compacte sous minimap |

### 4.7 Dialogue

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Speaker / texte / choix | Oui — `DialogueStatePush` | `DialoguePanel` + `SessionToken` + `ChoiceRequested` ; smokes `SpeakerTextForTest` / `ClickFirstChoiceForTest` | Overlay + portrait optionnel (sprite **si** déjà disponible, sinon placeholder) |

### 4.8 Magasin / banque / sol

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Achat / vente | Oui — catalogue + `ShopBuy` / `ShopSell` | Combos / `TrySelect*ForTest` / résultats log | Fenêtre Achat/Vente ; prix = données catalogue **si déjà là**, sinon qty seulement |
| Banque | Oui | List + or + `*ForTest` | Overlay ou onglet inventaire |
| Sol + ramasser | Oui | `GroundItemsSnapshot` + Pickup | Optionnel : pins carte plus tard (présentation) |

### 4.9 Options / login DA

| Élément | Fonctionne | Se conserve | Présentation-only |
| --- | --- | --- | --- |
| Options persistantes | **Non** | — | `UserSettings` JSON **nouveau** (volume, plein écran, taille fenêtre) — pas de paquet |
| Login / register / reconnect | Oui | Flux + jeton jamais logué | Skin sombre/or, même champs |

### 4.10 Combat / input (ne pas « habiller » en changeant la règle)

| Élément | Fonctionne | Se conserve |
| --- | --- | --- |
| Flèches + E interagir (400 ms) | Oui | `KeyPreview`, ignore si focus chat à traiter en UI-4 |
| Mêlée / sort / respawn | Oui | Cibles combo + paquets |
| Craft Guid | Oui (outil) | `CraftPanel` — polish recette catalogue = étape séparée, pas un nouveau craft serveur |

---

## 5. Contrats tests à ne pas casser

Smokes Windows (`tests/Frog.Editor.WindowsSmokeTests`) pilotent le chrome **via** `*ForTest` :

- `GameplayClientSmokeTests` — login, inventaire nommée, équipement `Arme: `, shop, banque, drop/pickup, chat
- `Phase8GameplayClientSmokeTests` / `Phase8ClientPanelRenderingTests` — onglet Quêtes, `DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel`, screenshots Phase 8

Règles pour toute étape UI ultérieure :

1. Garder les accesseurs `internal *ForTest` (les déplacer avec le contrôle, ne pas les supprimer).
2. Ne pas changer les chaînes assertées (`Arme: `, `Achat: Achat reussi.`, etc.) sans mettre à jour le smoke **dans la même étape**.
3. Les SHA de screenshots Phase 7/8 **changeront** dès le thème : l’étape qui change le chrome possède le manifeste.
4. Linux CI compile (`EnableWindowsTargeting`) mais **n’exécute pas** WinForms — preuve visuelle = job `windows-latest`.

---

## 6. Couleurs / DA actuelle (écart vs mockup)

| Zone | Actuel | Direction |
| --- | --- | --- |
| Login / perso | Bleu-gris clair `245,248,252` | Sombre + or, art château optionnel |
| Jeu | `SystemColors.Control` | Sombre, chrome semi-transparent |
| Carte fallback | Vert `60,90,60` ; joueur local or `240,200,60` | Monde pixel inchangé |
| Boutons | `StyleToolbarButton` (min 96×30) | Boutons or / sombres cohérents |

Aucun système de thème. Styles dupliqués dans `BuildLayout`.

---

## 7. Synthèse écart mockup ↔ produit

| Mockup | Produit | Action UI |
| --- | --- | --- |
| Carte plein écran + HUD flottant | Split 100% / 360 px + log | Layout overlay (UI-2+) |
| Barres HP/MP compactes HG | Texte dans onglet Gameplay | Relayer `CombatStateWire` |
| Minimap + tracker | Absents | Cache local + même journal |
| Chat canaux 5 + saisie overlay | 3 canaux + log bas | 3 canaux + onglet Système = log |
| Hotbar 1–0 | Boutons toolbar | Bind mêlée / sort / interact — **pas** nouveau combat |
| Inventaire grille + paper-doll | ListBox + 2 slots | Grille visuelle ; 2 slots réels |
| Fiche Force/Esprit/Guilde | 6 stats JSON cachées | Lecture seule ; pas de guilde |
| Options | Stub | Nouveau settings fichier |
| Login DA | Formulaire clair | Skin only |

**Rien de tout cela n’exige un changement serveur** si on reste sur les snapshots / paquets existants.
