# Référence — Client

← [Référence](../README.md)

Fonctions `FrogGameClient`, A–Z. Style `nom(args)`. Tip miroir : `d6e59759`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [clearinteractactivationid](#clearinteractactivationid) | `clearinteractactivationid()` | Efface l’activationId |
| [connectasync](#connectasync) | `connectasync(host, port)` | Ouvre le TCP |
| [disconnectasync](#disconnectasync) | `disconnectasync()` | Ferme le TCP |
| [sendbankdepositgoldasync](#sendbankdepositgoldasync) | `sendbankdepositgoldasync(amount)` | Dépose de l’or |
| [sendchatasync](#sendchatasync) | `sendchatasync(channel, whisperTarget, message)` | Envoie un chat |
| [sendcraftasync](#sendcraftasync) | `sendcraftasync(recipeId)` | Lance un craft |
| [sendinteractrequestasync](#sendinteractrequestasync) | `sendinteractrequestasync(activationId)` | Interaction tuile |
| [sendloginasync](#sendloginasync) | `sendloginasync(username, password)` | Authentifie |
| [sendmaprequestasync](#sendmaprequestasync) | `sendmaprequestasync(hintMapId?)` | Demande la carte |
| [sendmeleeattackasync](#sendmeleeattackasync) | `sendmeleeattackasync(targetUsername)` | Attaque mêlée |
| [sendpositionsyncasync](#sendpositionsyncasync) | `sendpositionsyncasync(pixelCenterX, pixelCenterY)` | Sync position pixel |
| [sendquestturninasync](#sendquestturninasync) | `sendquestturninasync(questId)` | Rend une quête |
| [sendsocialasync](#sendsocialasync) | `sendsocialasync(kind, action, requestId, extra)` | Enveloppe sociale 80 |
| [sendeconomyhubasync](sendeconomyhubasync.md) | `sendeconomyhubasync(kind, action, requestId, extra)` | Enveloppe économie 87 (Query-only) |
| [sendinstancehubasync](sendinstancehubasync.md) | `sendinstancehubasync(kind, action, requestId, extra)` | Enveloppe instance 90 |
| [sendspellcastasync](#sendspellcastasync) | `sendspellcastasync(spellId, targetName)` | Lance un sort |
| [soundservice-apply](soundservice-apply.md) | `apply(settings)` | Applique Son |
| [soundservice-applyweather](soundservice-applyweather.md) | `applyweather(plan)` | Gate météo mute |
| [soundservice-playuiclick](soundservice-playuiclick.md) | `playuiclick()` | SFX clic UI |
| [soundservice-syncmusic](soundservice-syncmusic.md) | `syncmusic()` | Boucle musique stub |

UI boutons : [MainShell](../../progress/phase-10-beta-release/guides/UI-CLIENT-MainShell.md) · [SocialHub](../../progress/phase-10-beta-release/guides/UI-CLIENT-SocialHub.md) (incl. échafaudage Courrier/HdV/Coffre/Instance) · [Options](../../progress/phase-10-beta-release/guides/UI-CLIENT-Options.md).

`WeatherOverlayRenderer` est **internal** (teinte + traits) — pas de fiche publique.

---


## EconomyHub / InstanceHub (#32 / #33 scaffolding)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [sendeconomyhubasync](sendeconomyhubasync.md) | `sendeconomyhubasync(kind, action, requestId, extra)` | Opcode 87 Query-only |
| [sendinstancehubasync](sendinstancehubasync.md) | `sendinstancehubasync(kind, action, requestId, extra)` | Opcode 90 Query/Enter/Leave |

**Honnêteté :** pas d’économie ni de donjon gameplay livrés — onglets vides / in-memory.

## SoundService (#28 / #30)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [apply](soundservice-apply.md) | `apply(settings)` | Settings → mixer |
| [playuiclick](soundservice-playuiclick.md) | `playuiclick()` | SFX clic |
| [syncmusic](soundservice-syncmusic.md) | `syncmusic()` | Boucle stub |
| [applyweather](soundservice-applyweather.md) | `applyweather(plan)` | Ambiance mute-friendly |

---

### clearinteractactivationid

Efface l’identifiant d’activation d’interaction en attente.

**Signature :** `clearinteractactivationid()`

**Entrées :** —

**Sorties :** —
*(état local client)*

---

### connectasync

Ouvre la connexion TCP vers le serveur.

*Source : `ConnectAsync`*

**Signature :** `connectasync(host, port)`

**Entrées :**
- `host` (`string`) — adresse du serveur
- `port` (`int`) — port d’écoute

**Sorties :**
- (`Task`) — complète quand connecté

**Refus :**
- hôte/port injoignable (exception réseau)

---

### disconnectasync

Ferme la connexion TCP.

**Signature :** `disconnectasync()`

**Entrées :** —

**Sorties :**
- (`Task`) — fermeture terminée
- événement `ConnectionClosed` possible

---

### sendbankdepositgoldasync

Dépose de l’or du personnage vers la banque.

**Signature :** `sendbankdepositgoldasync(amount)`

**Entrées :**
- `amount` (`int`) — quantité d’or (≥ 1)

**Sorties :**
- paquet `BankDepositRequest` envoyé
- résultat via événement `BankDepositResultReceived`

*Surcharge avec `requestId` (`Guid`) pour idempotence.*

---

### sendchatasync

Envoie un message sur un canal de chat.

**Signature :** `sendchatasync(channel, whisperTarget, message)`

**Entrées :**
- `channel` (`ChatChannel`) — Global / Map / Whisper
- `whisperTarget` (`string`) — destinataire si whisper
- `message` (`string`) — texte

**Sorties :**
- paquet `ChatSend` envoyé
- réception via `ChatMessageReceived`

**Refus :**
- canal hors 0–2 (serveur)

---

### sendcraftasync

Demande la fabrication d’une recette.

**Signature :** `sendcraftasync(recipeId)`

**Entrées :**
- `recipeId` (`Guid`) — recette publiée

**Sorties :**
- paquet `CraftRequest`
- `CraftResultReceived`

---

### sendinteractrequestasync

Déclenche l’interaction sur la tuile courante.

**Signature :** `sendinteractrequestasync(activationId)`

**Entrées :**
- `activationId` (`Guid`) — corrélation v10

**Sorties :**
- paquet `InteractRequest`
- `InteractResultReceived`

---

### sendloginasync

Authentifie un compte auprès du serveur.

*Source : `SendLoginAsync`*

**Signature :** `sendloginasync(username, password)`

**Entrées :**
- `username` (`string`) — identifiant compte
- `password` (`string`) — secret (ne jamais logger / coller en doc)

**Sorties :**
- paquet `LoginRequest`
- `LoginResultReceived(ok, message)`

---

### sendmaprequestasync

Demande les données de la carte (ou sync cache).

**Signature :** `sendmaprequestasync(hintMapId?)`

**Entrées :**
- `hintMapId` (`int?`) — hint de révision/cache

**Sorties :**
- `MapDataReceived` ou `MapAlreadySyncedReceived`

---

### sendmeleeattackasync

Envoie une attaque mêlée vers une cible.

**Signature :** `sendmeleeattackasync(targetUsername)`

**Entrées :**
- `targetUsername` (`string`) — nom de la cible

**Sorties :**
- `MeleeAttackResultReceived`

---

### sendpositionsyncasync

Synchronise le centre pixel du personnage.

*Source : `SendPositionSyncAsync`*

**Signature :** `sendpositionsyncasync(pixelCenterX, pixelCenterY)`

**Entrées :**
- `pixelCenterX` (`int`) — X monde
- `pixelCenterY` (`int`) — Y monde

**Sorties :**
- paquet `PositionSyncRequest`
- `PositionUpdateReceived` si accepté

---

### sendquestturninasync

Rend une quête (idempotent côté serveur).

**Signature :** `sendquestturninasync(questId)`

**Entrées :**
- `questId` (`Guid`) — quête

**Sorties :**
- `QuestTurnInResultReceived`
- éventuel `QuestJournalSnapshotReceived`

---

### sendsocialasync

Envoie une enveloppe sociale (opcode 80).

*Statut : livré (P10-1 + HUD #27)* · *Source : `SendSocialAsync`*

**Signature :** `sendsocialasync(kind, action, requestId, extra)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend / Block
- `action` (`byte`) — action dans la famille
- `requestId` (`Guid`) — corrélation
- `extra` (`ReadOnlySpan<byte>`) — payload typé

**Sorties :**
- paquet `SocialRequest` envoyé
- `SocialResultReceived` / `SocialSnapshotReceived` / `SocialEventReceived`

---

### sendspellcastasync

Lance un sort sur une cible.

**Signature :** `sendspellcastasync(spellId, targetName)`

**Entrées :**
- `spellId` (`Guid`) — sort catalogue
- `targetName` (`string`) — cible

**Sorties :**
- `SpellCastResultReceived`
