# Gel du protocole social et des échanges (avant développement)

**Statut :** figé en P10-0. Toute implémentation P10-1 / P10-2 doit respecter ce fichier. Un écart = mise à jour **explicite** de ce gel + justification, pas un opcode improvisé.

**Protocole actuel sur `main` :** `FrogWireProtocol.Version = 10`. **Cette branche :** `Version = 11`. P10-1 (**DONE** tip `dca2185`) : opcodes 80–83 + canaux Party/Guild. P10-2 : opcodes 84–86 (`TradeRequest` / `TradeResult` / `TradeSnapshot`) implémentés.

**Décision incompatible :** passer à **`FrogWireProtocol.Version = 11`** dès le premier commit produit qui ajoute un canal Party/Guild ou un opcode 80–86.

---

## 1. Version et refus des anciens clients

| Règle | Détail |
| --- | --- |
| Version Hello | **11** |
| Client v10 (Phase 9) | Refusé au Hello : message existant *« Version protocole incompatible (serveur indique 11, ce client attend 10)… »* — conserver le même libellé, valeurs à jour |
| Serveur v11 + client v10 | Pas de négociation, pas de mode compatible, pas de TLS optionnel pour « faire passer » l’ancien client |
| Transport bêta | TLS obligatoire sur le parcours externe (P10-5). Un client v11 en TCP clair vers un listener TLS échoue **avant** Hello — pas de repli silencieux en clair |
| Certificats de dev | Tests locaux uniquement ; le client bêta valide chaîne + nom |

Pourquoi v11 plutôt qu’une extension v10 : le parseur chat actuel n’accepte que 0/1/2 ; les testeurs externes doivent tous avoir le client social/trade/TLS ; le mandat exige une version explicite et un refus compréhensible.

---

## 2. Identités (jamais les noms affichés)

| Entité | Identité stable | Notes |
| --- | --- | --- |
| Compte | `auth.accounts.id` (Guid) | Sanctions, invitations de compte, rate-limit compte |
| Personnage | `player.characters.id` (Guid) | Toute appartenance sociale / trade |
| Groupe | `party_id` Guid éphémère (mémoire processus) | Non persisté PostgreSQL pour cette bêta |
| Guilde | `guild_id` Guid | Persisté |
| Échange | `trade_id` Guid | Persisté **après** commit seulement dans le journal ; l’offre en cours peut vivre en mémoire + lignes de réservation |
| Requête mutante | `request_id` Guid | Idempotence ; calquer `player.economy_request_ids` |

Les payloads réseau portent des Guid. Les noms ne servent qu’à l’affichage. Une homonymie ne doit pas adresser le mauvais personnage.

---

## 3. Opcodes prévus (v11)

Éviter une explosion d’`enum` et protéger `PacketDispatcher` (un seul rédacteur). Enveloppes multiplexées :

| ID | Nom | Sens | Corps |
| ---: | --- | --- | --- |
| 80 | `SocialRequest` | C→S | `kind` u8 + `action` u8 + `request_id` Guid + payload |
| 81 | `SocialResult` | S→C | `kind` + `action` + `request_id` + succès bool + message UTF-8 borné + ids concernés |
| 82 | `SocialSnapshot` | S→C | État liste (groupe **ou** guilde **ou** amis **ou** blocage) |
| 83 | `SocialEvent` | S→C | Poussée : invitation reçue, expiration, membre parti, chef transféré, présence |
| 84 | `TradeRequest` | C→S | `action` u8 + `trade_id` Guid + `request_id` Guid + payload |
| 85 | `TradeResult` | S→C | `action` + `trade_id` + `request_id` + succès + message |
| 86 | `TradeSnapshot` | S→C | Offre complète + `revision` u32 + drapeaux confirm des deux côtés |
| 255 | `Error` | S→C | inchangé |

`PacketId` 1–79 et 255 restent. Pas de réutilisation d’opcode.

Fichiers d’intégration : `Frog.Server/Network/PacketDispatcher.Social.cs` (P10-1), `PacketDispatcher.Trade.cs` (P10-2), codecs `Frog.Core/Protocol/SocialWire.cs`, `TradeWire.cs`.

### 3.1 `SocialKind`

| Valeur | Kind |
| ---: | --- |
| 1 | Party |
| 2 | Guild |
| 3 | Friend |
| 4 | Block |

### 3.2 Actions Party (`kind=1`)

| Valeur | Action | Payload principal |
| ---: | --- | --- |
| 1 | Invite | `target_character_id` |
| 2 | Accept | `party_id` (ou `invite_id`) |
| 3 | Decline | idem |
| 4 | Cancel | invitation sortante |
| 5 | Leave | — |
| 6 | Kick | `target_character_id` (chef seulement) |
| 7 | TransferLeader | `target_character_id` |
| 8 | Disband | confirmation booléenne |

### 3.3 Actions Guild (`kind=2`)

| Valeur | Action | Payload |
| ---: | --- | --- |
| 1 | Create | nom UTF-8 borné (≤ 32 caractères grapheme, ≤ 64 octets) |
| 2 | Invite | `target_character_id` |
| 3 | Accept | `guild_id` |
| 4 | Decline | `guild_id` |
| 5 | Leave | — |
| 6 | Kick | `target_character_id` (chef ou officier selon matrice) |
| 7 | TransferLeader | `target_character_id` (chef seulement) |
| 8 | Disband | confirmation (dernier membre **ou** chef d’une guilde vide de membres hors lui) |
| 9 | SetMotd | texte UTF-8 ≤ 256 octets (chef / officier) |

### 3.4 Actions Friend (`kind=3`)

| Valeur | Action |
| ---: | --- |
| 1 | Request |
| 2 | Accept |
| 3 | Decline |
| 4 | Remove |

### 3.5 Actions Block (`kind=4`)

| Valeur | Action |
| ---: | --- |
| 1 | Block |
| 2 | Unblock |

### 3.6 Actions Trade

| Valeur | Action | Payload |
| ---: | --- | --- |
| 1 | Invite | `target_character_id` |
| 2 | Accept | `trade_id` |
| 3 | Decline | `trade_id` |
| 4 | Cancel | `trade_id` |
| 5 | SetOffer | `revision_base` + listes d’items `(item_id, qty)` + or |
| 6 | Confirm | `revision` exacte |
| 7 | Unconfirm | — (optionnel : un `SetOffer` invalide déjà les confirms) |

Kind/action inconnus → `SocialResult` / `TradeResult` succès=false, message *« Action inconnue. »* (pas de crash, pas de mutation).

---

## 4. Canaux de chat

Étendre `Frog.Core/Enums/ChatChannel.cs` :

| Valeur | Canal | Destinataires |
| ---: | --- | --- |
| 0 | Global | inchangé |
| 1 | Map | inchangé |
| 2 | Whisper | inchangé ; **refusé** si l’expéditeur est bloqué par la cible |
| 3 | Party | membres du groupe du personnage actif uniquement |
| 4 | Guild | membres de la guilde uniquement |

Règles :

- Le mute serveur existant (`ops.account_sanctions`) s’applique **aussi** à Party et Guild, au même point que `ChatSend` aujourd’hui.
- Un non-membre qui envoie sur Party/Guild est rejeté (pas de fuite).
- Rate-limit chat : réutiliser `ChatRateLimiter` (8 / 10 s / session) — les nouveaux canaux comptent dans la même fenêtre.
- Pas de bouton « coffre de guilde » ni canal fantôme.

---

## 5. Groupes (temporaires)

| Paramètre | Valeur gelée |
| --- | --- |
| Capacité | **5** personnages |
| Appartenance | un seul groupe par personnage ; un seul chef |
| Invitation | TTL **60 s** par défaut (configurable serveur, défaut 60) |
| Persistance | **non** — redémarrage serveur → dissolution propre, message expliqué aux clients à la reconnexion |
| Reconnexion (processus vivant) | l’appartenance est retrouvée tant que le groupe existe |
| Butin / XP | **pas** de partage automatique ; les règles individuelles Phase 7–8 restent |
| Chef | quitter / expulsion avec permission / transfert / dissolution **avec confirmation** |

Invitations concurrentes vers le même personnage : la première acceptée gagne ; les autres deviennent caduques (refus serveur, pas de double appartenance). À capacité 5, toute invite supplémentaire est refusée.

---

## 6. Guildes (persistantes)

| Paramètre | Valeur gelée |
| --- | --- |
| Capacité | **50** (configurable serveur `Social:GuildMaxMembers`, défaut 50) |
| Appartenance | une guilde par personnage |
| Nom | unique **normalisé** : trim, compactage des blancs, comparaison insensible à la casse Unicode |
| Rôles | Chef, Officier, Membre — matrice ci-dessous **vérifiée serveur** |
| Persisté | appartenance, rôles, MOTD — PostgreSQL, survit reconnexion **et** redémarrage |
| Chef | doit transférer avant de quitter une guilde non vide |
| Dernier membre | peut dissoudre avec confirmation |
| Invariant | jamais 0 chef, jamais 2 chefs |
| Pouvoirs serveur | un rôle de guilde **n’accorde jamais** `IOperatorDirectory` |

### Matrice de permissions

| Action | Chef | Officier | Membre |
| --- | :---: | :---: | :---: |
| Inviter | oui | oui | non |
| Accepter / refuser une invite le concernant | oui | oui | oui |
| Kick membre | oui | oui (pas le chef, pas un autre officier) | non |
| Kick officier | oui | non | non |
| Transférer direction | oui | non | non |
| Set MOTD | oui | oui | non |
| Dissoudre | oui (si seul restant, ou confirmation explicite chef + guilde devenue vide de tiers) | non | dernier membre seulement s’il est aussi chef — en pratique le dernier est le chef |
| Canal guilde | oui | oui | oui |
| Quitter | oui si transfert d’abord ou guilde vide hors lui | oui | oui |

Tests minimum (mandat) : invitations concurrentes, capacité max, double acceptation, transfert simultané du chef (un seul gagne, transaction), membre expulsé pendant une action, permissions périmées, deux guildes distinctes, reconnexion, persistance redémarrage.

Schéma prévu (noms indicatifs, EF dans `Frog.Persistence.PostgreSql`) :

- `player.guilds` (id, normalized_name unique, motd, created_at)
- `player.guild_members` (guild_id, character_id unique, role, joined_at)
- `player.guild_invites` (id, guild_id, from_character_id, to_character_id, expires_at, status)

Ne **pas** partir de `Frog.Server/Models/Guild.cs` / `GuildService.cs`.

---

## 7. Amis et blocage

| Paramètre | Valeur gelée |
| --- | --- |
| Amitié | consentie ; persistante entre **personnages** |
| Présence | en ligne si session monde active ; hors ligne sinon |
| Blocage | persistant ; empêche whisper, invitations sociales, invitations d’échange **depuis** l’expéditeur bloqué |
| Capacité amis | **100** (défaut serveur, bêta) |
| Capacité blocage | **100** |

TTL demande d’ami : **7 jours** puis expiration (évite les files infinies). Suppression d’ami ≠ blocage.

---

## 8. Anti-spam invitations

| Limite | Valeur |
| --- | --- |
| Invitations sortantes **en attente** (tous kinds sociaux + trade, par personnage) | 3 |
| Invitations entrantes en attente | 8 |
| Re-invite vers la même cible après refus/expiration | cooldown **30 s** |
| `SocialRequest` + `TradeRequest` Invite | 10 / 60 s / personnage |
| Replay d’une invitation déjà acceptée / expirée / consommée | rejet + message visible |

Demandes falsifiées (mauvais `character_id`, invitation d’un tiers, permission périmée) → rejet serveur.

---

## 9. Échanges directs

| Paramètre | Valeur gelée |
| --- | --- |
| Participants | 2 personnages connectés, **vivants**, **même carte** |
| Distance max | **3 tuiles** → `3 * WorldMetrics.DefaultTileSizePixels` = **96 px** centre à centre (euclidien). Constante prévue `WorldMetrics.TradeRangePixels = 96` |
| Invitation | consentie, TTL **60 s** |
| Offre | items + or de chaque côté ; max **8** piles d’objets + or par côté |
| Révision | `revision` u32 monotone ; les deux clients affichent la **même** révision ; tout `SetOffer` valide incrémente et **invalide** les confirms |
| Validation finale | participants, distance, propriété, quantités, solde, capacité inventaire, disponibilité **au commit** |
| Concurrence | un objet offert ne peut pas partir deux fois (vente, banque, sol, équipement, conso, craft, autre trade) — **réservation** à `SetOffer` + contrôle de version au commit |
| Transaction | or, objets, deux inventaires, journal d’exécution = **une** transaction PostgreSQL |
| Replay | `request_id` du commit déjà validé → renvoyer le résultat, **sans** re-transfert (`EconomyRequestId` / opération `trade.commit`) |
| Avant commit | annulation, déconnexion, ban, changement de carte, expiration → **aucun** effet économique ; réservations libérées |
| Après commit | les biens ont le nouveau propriétaire même si la réponse TCP est perdue |
| Crash | ni objet perdu, ni duplication, ni réservation définitive (rollback PG + nettoyage à la reprise) |
| UI | confirmation **visible** : noms, icônes, quantités, or — pas de confirm aveugle sur une offre périmée |
| Journal admin | participants, `trade_id`, contenu, date ; **aucun secret** (pas de mot de passe, pas de jeton) |

`TradeSnapshot` est la seule source d’affichage de l’offre. Un client qui confirme une `revision` ≠ révision serveur est rejeté.

---

## 10. Timeouts (récap)

| Événement | Délai |
| --- | --- |
| Invite groupe | 60 s |
| Invite guilde | **120 s** |
| Demande d’ami | 7 jours |
| Invite échange | 60 s |
| Session d’échange sans mutation d’offre | 120 s → annulation sans effet |
| Groupe après restart serveur | dissolution immédiate au boot |
| Session idle joueur | 300 s (existant `Sessions:idleTimeoutSeconds`) — annule trade non committé via `SessionTeardown` |

---

## 11. Composants à réutiliser / à ne pas toucher sans besoin

Réutiliser : `SessionTeardown`, `ModerationService` (mute/ban), `IOperatorDirectory`, `ChatRateLimiter`, `player.economy_request_ids`, collisions/distance `WorldMetrics`, identités Guid personnage.

Ne pas : folklore `Role.cs` / `Permission.cs` / `AdminCommandService`, MariaDB, import `.fcc`, partage XP/butin improvisé, bouton coffre de guilde.

C2/C2b : le commit trade et les mutations d’appartenance passent par les mêmes verrous compte / personnage que le login ; un ban concurrent **avant** commit annule l’échange.

---

## 12. Ce que ce gel n’autorise pas

- Implémenter P10-1/P10-2 dans le lot P10-0.
- Changer v11 sans mettre à jour ce fichier.
- Servir un ancien client « en lecture seule ».
- Annoncer un partage de butin ou un coffre de guilde.
