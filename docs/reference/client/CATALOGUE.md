# Frog.Client — référence API (`FrogGameClient`)

← [Référence](../README.md) · [Client](./README.md)

Tip : `6fe5bd97`. Fichier : `Frog.Client/Network/FrogGameClient.cs`.

Événements reçus (S→C) : voir propriétés `*Received` sur la même classe (non listés exhaustivement ici — enrichir au fil de l’eau).

---

### ClearEconomyRequestId
Oublie le `requestId` idempotent d’une opération économie.

**Signature :** `void ClearEconomyRequestId(string operation)`

**Entrées :**
- `operation` (string) — clé d’opération (ex. dépôt/retrait)

**Sorties :**
- — (état client local mis à jour)

---

### ClearInteractActivationId
Efface l’`activationId` d’interaction en attente.

**Signature :** `void ClearInteractActivationId()`

**Entrées :** —

**Sorties :** —

---

### ConnectAsync
Ouvre le TCP vers le serveur.

**Signature :** `Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)`

**Entrées :**
- `host` (string) — adresse
- `port` (int) — port TCP
- `cancellationToken` (CancellationToken) — annulation

**Sorties :**
- Task — complète à la connexion ; lève si échec
- événements ultérieurs (`HelloReceived`, etc.)

---

### DisconnectAsync
Ferme la connexion.

**Signature :** `Task DisconnectAsync()`

**Entrées :** —

**Sorties :**
- Task ; peut lever `ConnectionClosed`

---

### Dispose
Libère ressources réseau.

**Signature :** `void Dispose()`

**Entrées :** —

**Sorties :** —

---

### IsConnected
Indique si le TCP est connecté.

**Signature :** `bool IsConnected { get; }`

**Entrées :** —

**Sorties :**
- (bool) — `true` si `_tcp.Connected`

---

### PeekEconomyRequestId
Lit le `requestId` mémorisé pour une opération.

**Signature :** `bool PeekEconomyRequestId(string operation, out Guid requestId)`

**Entrées :**
- `operation` (string)

**Sorties :**
- (bool) — trouvé ?
- `requestId` (Guid) — id si trouvé

---

### PeekInteractActivationId
Lit l’`activationId` courant pour Interact.

**Signature :** `bool PeekInteractActivationId(out Guid activationId)`

**Entrées :** —

**Sorties :**
- (bool)
- `activationId` (Guid)

---

### SendAcquireProfessionAsync
Demande l’acquisition d’un métier publié.

**Signature :** `Task SendAcquireProfessionAsync(Guid professionId, CancellationToken cancellationToken = default)`

**Entrées :**
- `professionId` (Guid) — métier catalogue

**Sorties :**
- paquet `AcquireProfessionRequest` ; résultat via `AcquireProfessionResultReceived`

---

### SendBankDepositGoldAsync
Dépose de l’or en banque.

**Signature :** `Task SendBankDepositGoldAsync(int amount, CancellationToken cancellationToken = default)`
surcharge : `(int amount, Guid requestId, …)`

**Entrées :**
- `amount` (int) — or ≥ 1
- `requestId` (Guid, opt.) — idempotence

**Sorties :**
- `BankDepositResultReceived` / `BankSnapshotReceived`

---

### SendBankDepositItemAsync
Dépose un item d’inventaire en banque.

**Signature :** `Task SendBankDepositItemAsync(byte slotIndex, int quantity, …)`

**Entrées :**
- `slotIndex` (byte) — slot inventaire
- `quantity` (int) — quantité

**Sorties :**
- résultats banque

---

### SendBankWithdrawGoldAsync / SendBankWithdrawItemAsync
Retrait or / item depuis la banque.

**Signature :** analogues aux dépôts

**Entrées :** `amount` ou `slotIndex`+`quantity` ; `requestId` optionnel

**Sorties :** résultats banque

---

### SendCharacterCreateAsync
Crée un personnage.

**Signature :** `Task SendCharacterCreateAsync(string displayName, CancellationToken …)`
surcharge : `(string displayName, Guid? classId, …)`

**Entrées :**
- `displayName` (string) — nom affiché
- `classId` (Guid?) — classe optionnelle

**Sorties :**
- `CharacterCreateResultReceived`

---

### SendCharacterListRequestAsync
Demande la liste des personnages du compte.

**Signature :** `Task SendCharacterListRequestAsync(CancellationToken cancellationToken = default)`

**Entrées :** —

**Sorties :**
- `CharacterListReceived` (JSON)

---

### SendCharacterSelectAsync
Choisit le personnage actif.

**Signature :** `Task SendCharacterSelectAsync(string characterId, CancellationToken cancellationToken = default)`

**Entrées :**
- `characterId` (string) — UUID perso

**Sorties :**
- `CharacterSelectResultReceived`

---

### SendCharacterStatsUpdateAsync
Envoie les 6 stats packées (STR…LUCK).

**Signature :** `Task SendCharacterStatsUpdateAsync(ReadOnlySpan<byte> packedSixStats, CancellationToken …)`

**Entrées :**
- `packedSixStats` (ReadOnlySpan<byte>) — 6 octets 1–99

**Sorties :**
- `CharacterStatsUpdateResultReceived`

---

### SendChatAsync
Envoie un message chat.

**Signature :** `Task SendChatAsync(ChatChannel channel, string whisperTarget, string message, CancellationToken …)`

**Entrées :**
- `channel` (ChatChannel) — Global/Map/Whisper (0–2)
- `whisperTarget` (string) — cible si whisper
- `message` (string) — texte

**Sorties :**
- `ChatMessageReceived` (réception)

---

### SendConnect… → see ConnectAsync

---

### SendCraftAsync
Lance un craft.

**Signature :** `Task SendCraftAsync(Guid recipeId, …)` (+ surcharge `requestId`)

**Entrées :**
- `recipeId` (Guid)
- `requestId` (Guid, opt.)

**Sorties :**
- `CraftResultReceived`

---

### SendDialogueChoiceAsync
Choisit une option de dialogue.

**Signature :** `Task SendDialogueChoiceAsync(byte[] sessionToken, string choiceId, CancellationToken …)`

**Entrées :**
- `sessionToken` (byte[]) — token session dialogue
- `choiceId` (string) — choix

**Sorties :**
- `DialogueChoiceResultReceived` / `DialogueStatePushReceived`

---

### SendDropItemAsync
Jette un item au sol.

**Signature :** `Task SendDropItemAsync(byte slotIndex, int quantity, CancellationToken …)`

**Entrées :**
- `slotIndex` (byte)
- `quantity` (int)

**Sorties :**
- `DropItemResultReceived`

---

### SendEquipAsync / SendUnequipAsync
Équipe / déséquipe.

**Signature :** `Task SendEquipAsync(byte slotIndex, …)` · `Task SendUnequipAsync(EquipmentSlotKind slot, …)`

**Entrées :** slot inventaire ou slot équipement

**Sorties :**
- `EquipResultReceived` / `UnequipResultReceived`

---

### SendHeartbeatAsync
Keepalive.

**Signature :** `Task SendHeartbeatAsync(CancellationToken cancellationToken = default)`

**Entrées :** —

**Sorties :**
- `HeartbeatAckReceived`

---

### SendInteractRequestAsync
Interagit sur la tuile (événements).

**Signature :** `Task SendInteractRequestAsync(CancellationToken …)`
`Task SendInteractRequestAsync(Guid activationId, …)`

**Entrées :**
- `activationId` (Guid) — corrélation v10 (auto si overload sans arg)

**Sorties :**
- `InteractResultReceived`

---

### SendLoginAsync
Authentifie le compte.

**Signature :** `Task SendLoginAsync(string username, string password, CancellationToken …)`

**Entrées :**
- `username` (string)
- `password` (string) — ne pas logger / documenter d’exemple réel en prod

**Sorties :**
- `LoginResultReceived(bool ok, string message)`

---

### SendLogoutAsync
Demande logout.

**Signature :** `Task SendLogoutAsync(CancellationToken cancellationToken = default)`

**Entrées :** —

**Sorties :**
- `LogoutAckReceived`

---

### SendMapEventsRequestAsync
Demande les événements de la carte courante.

**Signature :** `Task SendMapEventsRequestAsync(CancellationToken …)`

**Entrées :** —

**Sorties :**
- `MapEventsResultReceived`

---

### SendMapRequestAsync
Demande les données de carte.

**Signature :** `Task SendMapRequestAsync(…)` · `SendMapRequestAsync(int? hintMapId, …)` · `SendMapRequestIgnoringFingerprintAsync`

**Entrées :**
- `hintMapId` (int?) — hint cache

**Sorties :**
- `MapDataReceived` ou `MapAlreadySyncedReceived`

---

### SendMeleeAttackAsync
Attaque mêlée.

**Signature :** `Task SendMeleeAttackAsync(string targetUsername, CancellationToken …)`

**Entrées :**
- `targetUsername` (string) — cible

**Sorties :**
- `MeleeAttackResultReceived`

---

### SendModerateAsync
Commande opérateur mute/kick/ban.

**Signature :** `Task SendModerateAsync(…)` — voir surcharges dans le source

**Entrées :** action + cible (ModerateWire)

**Sorties :**
- `ModerateResultReceived`

---

### SendMoveAsync
Déplacement relatif grille.

**Signature :** `Task SendMoveAsync(sbyte dx, sbyte dy, CancellationToken …)`

**Entrées :**
- `dx`, `dy` (sbyte)

**Sorties :**
- `PositionUpdateReceived`

---

### SendPickupItemAsync
Ramasse un item au sol.

**Signature :** `Task SendPickupItemAsync(Guid groundItemId, CancellationToken …)`

**Entrées :**
- `groundItemId` (Guid)

**Sorties :**
- `PickupItemResultReceived`

---

### SendPositionSyncAsync
Sync centre pixel monde.

**Signature :** `Task SendPositionSyncAsync(int pixelCenterX, int pixelCenterY, CancellationToken …)`

**Entrées :**
- `pixelCenterX`, `pixelCenterY` (int)

**Sorties :**
- `PositionUpdateReceived` si accepté

---

### SendPublishedCatalogRequestAsync
Demande le catalogue publié.

**Signature :** `Task SendPublishedCatalogRequestAsync(CancellationToken …)`

**Entrées :** —

**Sorties :**
- `PublishedCatalogReceived`

---

### SendQuestTurnInAsync
Rend une quête.

**Signature :** `Task SendQuestTurnInAsync(Guid questId, …)` (+ `requestId`)

**Entrées :**
- `questId` (Guid)

**Sorties :**
- `QuestTurnInResultReceived` / journal

---

### SendReconnectAsync
Reconnecte avec jeton opaque.

**Signature :** `Task SendReconnectAsync(string token, CancellationToken …)`

**Entrées :**
- `token` (string) — jeton (ne pas publier d’exemple réel)

**Sorties :**
- `ReconnectResultReceived`

---

### SendRegisterAsync
Crée un compte.

**Signature :** `Task SendRegisterAsync(string username, string password, CancellationToken …)`

**Entrées :**
- `username`, `password` (string)

**Sorties :**
- `RegisterResultReceived`

---

### SendRespawnAsync
Demande respawn.

**Signature :** `Task SendRespawnAsync(CancellationToken …)`

**Entrées :** —

**Sorties :**
- `RespawnResultReceived`

---

### SendShopBuyAsync / SendShopSellAsync
Achat / vente boutique PNJ.

**Signature :** `SendShopBuyAsync(Guid shopId, Guid itemId, int quantity, …)` · `SendShopSellAsync(byte slotIndex, int quantity, …)`

**Entrées :** ids boutique/item ou slot + qty

**Sorties :**
- `ShopBuyResultReceived` / `ShopSellResultReceived`

---

### SendSocialAsync
Envoie une enveloppe sociale (80).

**Signature :** `Task SendSocialAsync(…)` — voir source (kind/action/requestId/extra)

**Entrées :**
- kind / action / payload social

**Sorties :**
- `SocialResultReceived` / `SocialSnapshotReceived` / `SocialEventReceived`

---

### SendSpellCastAsync
Lance un sort.

**Signature :** `Task SendSpellCastAsync(Guid spellId, string targetName, CancellationToken …)`

**Entrées :**
- `spellId` (Guid)
- `targetName` (string)

**Sorties :**
- `SpellCastResultReceived`

---

### SendWorldFlagsPatchAsync
Patch JSON des flags monde (souvent refusé en PG prod).

**Signature :** `Task SendWorldFlagsPatchAsync(string patchJsonObject, CancellationToken …)`

**Entrées :**
- `patchJsonObject` (string) — objet JSON booléens

**Sorties :**
- `WorldFlagsPatchResultReceived`

---

### SetMapRequestHintMapId
Fixe le hint de map pour la prochaine demande.

**Signature :** `void SetMapRequestHintMapId(int mapId)`

**Entrées :**
- `mapId` (int)

**Sorties :** —
