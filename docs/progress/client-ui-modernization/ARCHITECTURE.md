# Architecture UI — overlays compatibles (Frog.Client)

**Propriétaire :** Netsun  
**Statut :** proposition pour les étapes UI-1…UI-15. **Aucun** de ces types n’est implémenté dans ce run.  
**Base auditée :** `f74b34c` — voir [BASELINE_AUDIT.md](BASELINE_AUDIT.md).

Objectif : moderniser la **présentation** sans recoupler le TCP, sans réécrire le gameplay, sans toucher Phase 10.

---

## 1. Principes

1. **`FrogGameClient` reste la seule I/O réseau.** Les panneaux ne parlent pas au socket.
2. **Les snapshots / events existants restent la source de vérité UI** (`CombatStateWire`, `InventorySnapshotWire`, `QuestJournalEntryWire`, `DialogueStateWire`, `BankSnapshotWire`, `GroundItemsSnapshotWire`, `EnvironmentStateWire`, `ChatMessageReceived`, catalogue publié).
3. **Un contrôle = présentation + events d’intention** (`EquipRequested`, `ChoiceRequested`, …). `MainShellForm` (ou un futur `ClientSessionController` extrait) traduit l’intention en `Send*Async`.
4. **La carte ne dépend pas des panneaux.** `MapViewRenderer` + timer 16 ms restent un pipeline à part. Invalider un overlay **ne doit pas** forcer `RedrawMap()`.
5. **Thème = jetons**, pas de couleurs magiques dans chaque bouton.
6. **Stubs `// TODO` :** ne pas les « remplir pour le nom ». Nouveaux types sous `Frog.Client/UI/` (ou remplacement atomique d’un fichier stub).
7. **ADR-0003 / ADR-0004 :** pas de parité VB6 ; pas d’extension WPF sur le client joueur (le client est déjà WinForms pur — le garder ainsi).
8. **Mockup = direction**, pas spec pixel. Canaux / slots / stats absents du wire restent absents.

---

## 2. Couches

```text
┌─ Presentation (WinForms) ─────────────────────────────────────────┐
│  MainShellForm          phases Login | CharacterSelect | Playing    │
│  OverlayHost            Z-order HUD / fenêtres / modal              │
│  UiTheme                couleurs, polices, métriques chrome         │
│  Panels                 vitals, chat, inv, quest, shop, …           │
└───────────────────────────────┬───────────────────────────────────┘
                                │ events + Apply*(snapshot)
┌─ Session (code-behind puis extract) ──────────────────────────────┐
│  MainShellForm aujourd’hui : bind FrogGameClient ↔ panels          │
│  Plus tard (optionnel, étape dédiée) : ClientSessionFacade         │
│    - pas de nouveau protocole                                      │
│    - juste sortir les 3k lignes de layout hors du Form             │
└───────────────────────────────┬───────────────────────────────────┘
                                │ Send* / events
┌─ Network (inchangé) ──────────────────────────────────────────────┐
│  FrogGameClient + TcpFrameCodec + Frog.Core.Protocol               │
└───────────────────────────────────────────────────────────────────┘
┌─ World render (inchangé fonctionnellement) ───────────────────────┐
│  MapViewRenderer → Bitmap ; _smoothTimer ; tilesets                │
└───────────────────────────────────────────────────────────────────┘
```

`Frog.Client` continue de référencer `Frog.Core` + `Frog.Application` (playtest CLI seulement). **Pas** de référence `Frog.Server` / persistence depuis l’UI.

---

## 3. Overlay host (WinForms, contraintes réelles)

WinForms n’a pas de vrai calque transparent cheap. Trois modes, du plus sûr au plus risqué :

| Mode | Idée | Quand | Risque 60 FPS |
| --- | --- | --- | --- |
| **A — Dock chrome** | Garder split carte / rail, mais skin sombre/or | UI-1 | Faible |
| **B — Overlay opaque ancré** | `Panel`s `Location` sur un host `Dock.Fill` **au-dessus** de la carte, fonds opaques / alpha simulé (couleur sombre pleine) | UI-2…UI-8 | Moyen si `Invalidate(true)` sur le Form |
| **C — TransparencyKey / layered** | Vrai voir-à-travers | Interdit en première intention | Élevé (invalidation carte sous-jacente) |

**Décision recommandée :** A puis B. Les fenêtres (inventaire, quêtes) sont des `UserControl` draggables **opaques** (bordure or). Le HUD HG/BD/BG est ancré, pas une fenêtre libre. La carte reste un sibling dessous, pas un enfant invalidé par le chrome.

Contrat `IOverlayHost` (à introduire en UI-2) :

```text
Register(anchor, control)     // TopLeft, TopRight, BottomLeft, BottomCenter, BottomRight, CenterModal
Show(id) / Hide(id) / Toggle
IsTextInputFocused            // pour ne pas manger les flèches
SetMapExclusiveInput(bool)
```

Z-order : carte < HUD ancré < fenêtres < modal (dialogue, options, mort).

Ne **pas** mettre le `PictureBox` carte dans le même parent que des contrôles qui font `SuspendLayout` global à 60 Hz.

---

## 4. Thème sombre + or

Nouveau type proposé : `Frog.Client/UI/UiTheme.cs` (UI-1).

| Jeton | Usage |
| --- | --- |
| `BackgroundDeep` | Fond form / login |
| `PanelFill` | Corps fenêtre |
| `PanelHeader` | Barre titre |
| `BorderGold` | Contour 1 px |
| `TextPrimary` / `TextMuted` | Labels |
| `HpFill` / `MpFill` / `XpFill` | Barres (données `CombatStateWire`) |
| `AccentGold` | Bouton primaire, slot sélectionné |
| `ChatGlobal` / `ChatMap` / `ChatWhisper` / `ChatSystem` | Couleurs lignes |

Règles :

- `UiTheme.Apply(Control)` récursif **une fois** à la construction / changement de thème — pas par frame.
- Polices : `Segoe UI` / `MessageBoxFont` ; tailles HUD compactes. Pas d’assets ornementaux bloquants.
- Le **monde** (`MapViewRenderer` couleurs fallback, nearest-neighbor) n’utilise **pas** `UiTheme` (pixel-art conservé).

Référence visuelle : mockup fourni avec le mandat (navy + or). Valeurs RGB exactes = choix UI-1, pas clone.

---

## 5. Mapping panneaux → services existants

Pas de nouveaux `*Service` métier. Table de binding :

| Surface overlay | Contrôle live à réutiliser / extraire | Source | Intention → |
| --- | --- | --- | --- |
| PlayerHud | nouveau (texte `_lblCombat` aujourd’hui) | `CombatStateReceived` | Respawn button |
| ChatDock | extraire saisie + **nouveau** historique (sortir le chat de `_txtLog`) | `ChatMessageReceived` ; slash `ModerateWire` | `SendChatAsync` |
| InventoryWindow | `InventoryPanel` + or HUD | `InventorySnapshot` + `CombatState.Gold` | Equip / Drop |
| EquipmentPane | `EquipmentPanel` | même snapshot | Unequip |
| CharacterSheet | nouveau lecture seule | payload JSON stats + combat + nom | aucun send (édition reste masquée) |
| QuestWindow + QuestTracker | `QuestJournalPanel` + filtre | `QuestJournalSnapshot` | Turn-in |
| DialogueModal | `DialoguePanel` | `DialogueStatePush` | `SendDialogueChoiceAsync` |
| ShopWindow | extraire combos shop | catalogue + buy/sell results | `SendShopBuy/Sell` |
| BankPane | listes existantes | `BankSnapshot` | deposit/withdraw |
| GroundPane | liste sol | `GroundItemsSnapshot` | Pickup |
| CraftPane | `CraftPanel` | résultat craft | `SendCraft` |
| EnvironmentHud | `EnvironmentPanel` (compact) | `EnvironmentStatePush` | — |
| OptionsWindow | **nouveau** (pas le stub vide) | fichier local | — |
| MiniMap | **nouveau** | `_map` + positions déjà en mémoire | — |
| Hotbar | **nouveau** | boutons mêlée / sort / E | mêmes `Send*` |
| Login / Char | panels existants | auth / character list | inchangé |

`_txtLog` reste un **journal debug / système** (erreurs, XP, résultats). Le chat joueur ne doit plus dépendre uniquement du log une fois UI-4 livré, mais le log peut rester (smokes `LogContainsForTest`).

---

## 6. Données que l’UI ne doit pas inventer

| Tentation mockup | Réalité wire | Conduite |
| --- | --- | --- |
| Canaux Guilde / Groupe | `ChatChannel` = Global, Map, Whisper | 3 onglets + Système (local). Pas d’onglet Guilde cliquable « pour plus tard » qui ment. |
| 8+ slots paper-doll | Weapon + Armor | 2 slots actifs ; le reste non dessiné ou clairement « — » non interactif **interdit** (évite faux équipement). Préférer layout 2 slots soigné. |
| Stats Esprit / Endurance / Réputation | STR AGI DEX INT VIT LUCK | Alias d’affichage optionnels, 6 valeurs. |
| Prix boutique fancy | Catalogue items + shop item ids | Afficher nom ; prix seulement si le catalogue le porte déjà. |
| Options vsync / 1280×720 serveur | Rien | Settings **client** : taille fenêtre, plein écran WinForms, volume no-op tant que `SoundService` est stub. |

---

## 7. Input

Aujourd’hui : `KeyPreview` + flèches / E même si un TextBox a le focus (risque de bouger en tapant le chat selon focus — à vérifier en UI-4).

Contrat cible :

- Si `OverlayHost.IsTextInputFocused` → ne pas poser `_holdLeft` etc. ; Enter envoie le chat.
- Raccourcis fenêtres (I inventaire, J journal, C fiche, O options, Esc ferme) = **présentation**, mappés localement. Pas de `KeyBindings` serveur.
- E / mêlée / sorts inchangés.

---

## 8. Tests et extractibilité

Garder `InternalsVisibleTo` + accesseurs sur `MainShellForm`. Si un panneau bouge dans un overlay, le getter pointe le **même type**.

Extract `ClientSessionFacade` **seulement** si une étape de layout rend `BuildLayout` ingérable — ce n’est **pas** un prérequis UI-1. Quand on extraie : tests compilent, aucun changement de paquet.

Screenshots : nouveau dossier `artifacts/client-ui-modernization/` pour les preuves chrome ; **ne pas** écraser les SHA Phase 8 tant que l’étape n’assume pas le manifeste.

---

## 9. Ce que cette architecture refuse

- Nouveau opcode / champ wire « pour le HUD »
- Recalcul HP/MP/or côté client
- Deuxième client TCP
- WPF / Avalonia / MonoGame « pour faire joli » dans ce chantier (changement de stack = autre décision, hors plan)
- Remplir `Services/GameLoop.cs` stub en parallèle du `_smoothTimer` live (double boucle)
- Modifier `Frog.Server` ou `cursor/phase10-beta-release`
