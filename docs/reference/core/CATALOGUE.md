# Frog.Core — référence API

← [Référence](../README.md) · [Core](./README.md)

Tip : `6fe5bd97`. Commun client/serveur.

---

### CombatFormulas.BasicAttackCooldownMs
Délai minimal entre deux attaques de base.

**Signature :** `const int BasicAttackCooldownMs`

**Entrées :** — (constante)

**Sorties :**
- `800` (int) — millisecondes de cooldown mêlée

---

### CombatFormulas.BasicAttackRangePixels
Portée mêlée en pixels monde.

**Signature :** `const int BasicAttackRangePixels`

**Entrées :** —

**Sorties :**
- `56` (int) — portée

---

### CombatFormulas.DefaultSpellRangePixels
Portée sort par défaut.

**Signature :** `const int DefaultSpellRangePixels`

**Entrées :** —

**Sorties :**
- `160` (int)

---

### CombatFormulas.MeleeDamage
Calcule les dégâts mêlée.

**Signature :** `static int MeleeDamage(int attackerStr, int weaponPower, int targetVit)`

**Entrées :**
- `attackerStr` (int) — force de l’attaquant
- `weaponPower` (int) — puissance arme
- `targetVit` (int) — vitalité cible

**Sorties :**
- (int) — points de dégâts

---

### CombatFormulas.MonsterExperienceReward
XP accordée pour un monstre de niveau donné.

**Signature :** `static long MonsterExperienceReward(int level)`

**Entrées :**
- `level` (int) — niveau monstre

**Sorties :**
- (long) — expérience

---

### CombatFormulas.MonsterMaxHp
PV max monstre selon niveau.

**Signature :** `static int MonsterMaxHp(int level)`

**Entrées :**
- `level` (int)

**Sorties :**
- (int) — PV max

---

### CombatFormulas.SpellDamage
Dégâts de sort.

**Signature :** `static int SpellDamage(int attackerInt, int spellPower, int targetVit)`

**Entrées :**
- `attackerInt` (int) — intelligence
- `spellPower` (int) — puissance sort
- `targetVit` (int) — vitalité cible

**Sorties :**
- (int) — dégâts

---

### CombatFormulas.SpellPowerFromManaCost
Derive la puissance affichée depuis le coût mana.

**Signature :** `static int SpellPowerFromManaCost(int manaCost)`

**Entrées :**
- `manaCost` (int)

**Sorties :**
- (int) — puissance

---

### FrogWireProtocol.Version
Version incompatible du contrat TCP (Hello).

**Signature :** `const ushort Version`

**Entrées :** —

**Sorties :**
- `10` (ushort) — valeur actuelle sur le tip ; social 80–83 peut imposer **11** (voir gel)

---

### PacketId (enum)
Identifiants d’opcodes filaires (byte).

**Signature :** `enum PacketId : byte`

**Entrées :** — (énumération)

**Sorties :** symboles A–Z (extrait) :

| Symbole | Valeur | Rôle court |
| --- | --- | --- |
| `AcquireProfessionRequest` / `Result` | 75 / 76 | Acquérir un métier |
| `BankDepositRequest` / `Result` | 55 / 56 | Dépôt banque |
| `BankSnapshot` | 59 | État banque |
| `BankWithdrawRequest` / `Result` | 57 / 58 | Retrait banque |
| `CharacterCreateRequest` / `Result` | 25 / 26 | Créer perso |
| `CharacterListRequest` / `Result` | 21 / 22 | Liste persos |
| `CharacterPayload` | 20 | Stats/flags perso |
| `CharacterSelectRequest` / `Result` | 23 / 24 | Choisir perso |
| `CharacterStatsUpdateRequest` / `Result` | 27 / 28 | Stats 6 octets |
| `ChatMessage` / `ChatSend` | 16 / 15 | Chat |
| `CombatState` | 50 | HP/MP/XP/or |
| `CraftRequest` / `Result` | 72 / 73 | Craft |
| `DeathNotify` | 63 | Mort |
| `DialogueChoiceRequest` / `Result` | 67 / 68 | Choix dialogue |
| `DialogueStatePush` | 66 | État dialogue |
| `DropItemRequest` / `Result` | 43 / 44 | Jeter item |
| `EnvironmentStatePush` | 74 | Région/météo |
| `EquipRequest` / `Result` | 39 / 40 | Équiper |
| `Error` | 255 | Erreur générique |
| `ExperienceGain` | 62 | Gain XP |
| `GroundItemsSnapshot` | 47 | Sol |
| `HeartbeatRequest` / `Ack` | 11 / 12 | Keepalive |
| `Hello` | 1 | Version + message |
| `InteractRequest` / `Result` | 31 / 32 | Interaction (`activationId`) |
| `InventorySnapshot` | 38 | Inventaire |
| `LoginRequest` / `Result` | 2 / 3 | Auth |
| `LogoutRequest` / `Ack` | 13 / 14 | Déconnexion |
| `MapAlreadySynced` | 19 | Cache map OK |
| `MapData` / `MapRequest` | 5 / 4 | Carte |
| `MapEventsRequest` / `Result` | 29 / 30 | Événements carte |
| `MeleeAttackRequest` / `Result` | 17 / 18 | Mêlée |
| `ModerateRequest` / `Result` | 78 / 79 | Mute/kick/ban |
| `MoveRequest` | 8 | Pas grille |
| `PickupItemRequest` / `Result` | 45 / 46 | Ramasser |
| `PlayerLeave` | 10 | Départ joueur |
| `PositionSyncRequest` / `PositionUpdate` | 33 / 9 | Sync pixel / broadcast |
| `PublishedCatalogRequest` / `Result` | 64 / 65 | Catalogue publié |
| `QuestJournalSnapshot` | 69 | Journal quêtes |
| `QuestTurnInRequest` / `Result` | 70 / 71 | Rendre quête |
| `ReconnectRequest` / `Result` | 36 / 37 | Jeton reconnect |
| `RegisterRequest` / `Result` | 6 / 7 | Inscription |
| `RespawnRequest` / `Result` | 60 / 61 | Respawn |
| `ShopBuyRequest` / `Result` | 51 / 52 | Achat PNJ |
| `ShopSellRequest` / `Result` | 53 / 54 | Vente PNJ |
| `SocialEvent` / `Request` / `Result` / `Snapshot` | 83 / 80 / 81 / 82 | Social v11 enveloppes |
| `SpellCastRequest` / `Result` | 48 / 49 | Sort |
| `UnequipRequest` / `Result` | 41 / 42 | Déséquiper |
| `WorldFlagsPatchRequest` / `Result` | 34 / 35 | Patch flags (souvent rejeté PG) |
| `WorldSwitchSnapshot` | 77 | Interrupteurs perso |

---

### SocialKind
Famille d’une enveloppe sociale (80–83).

**Signature :** `enum SocialKind : byte`

**Entrées :** —

**Sorties :**
- `Party = 1`, `Guild = 2`, `Friend = 3`, `Block = 4`

---

### SocialProtocolLimits
Bornes UTF-8 / effectifs social.

**Signature :** `static class SocialProtocolLimits` (constantes)

**Entrées :** —

**Sorties (extrait) :**
- `PartyMaxMembers` (int) — `5`
- `GuildMaxMembersDefault` (int) — `50`
- `MaxFriendsDefault` / `MaxBlocksDefault` — `100`
- `MaxResultMessageUtf8Bytes` — `256`
- délais invite / rate (voir type)

---

### SocialWire.BuildRequest
Construit le corps d’une `SocialRequest` (80).

**Signature :** `static byte[] BuildRequest(SocialKind kind, byte action, Guid requestId, ReadOnlySpan<byte> extra)`

**Entrées :**
- `kind` (SocialKind) — famille
- `action` (byte) — action dans la famille
- `requestId` (Guid) — corrélation
- `extra` (ReadOnlySpan<byte>) — payload typé

**Sorties :**
- (byte[]) — frame corps

---

### SocialWire.TryParseRequest
Parse une SocialRequest.

**Signature :** `static bool TryParseRequest(ReadOnlySpan<byte> payload, out SocialKind kind, out byte action, out Guid requestId, out ReadOnlySpan<byte> extra)`

**Entrées :**
- `payload` (ReadOnlySpan<byte>) — corps paquet

**Sorties :**
- (bool) — `true` si parse OK
- `kind`, `action`, `requestId`, `extra` — champs extraits
