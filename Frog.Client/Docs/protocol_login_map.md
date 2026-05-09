# Protocole Sprint 2: Login + Map

Ce document decrit le protocole reseau minimal utilise entre le client et le serveur pour le flux:

1. connexion TCP
2. handshake `Hello` (avec version protocole)
3. login
4. demande de map
5. reception de map

Le transport est base sur des *frames* binaires.

## Version protocole (Hello)

La constante partagee **`FrogWireProtocol.Version`** (UInt16) est definie dans `Frog.Core/Constants/FrogWireProtocol.cs`.  
Le serveur l’envoie dans le corps du paquet `Hello` (voir plus bas). Le client doit la lire avant tout autre et **ferme la TCP** avec un message d’erreur si la valeur differe du client reel.

**Politique projet :** client et serveur deployes ensembles (meme depot / meme build). Toute rupture doit incrémenter `FrogWireProtocol.Version` ET mettre ce document a jour.

## Encapsulation des frames

Chaque message reseau est encode ainsi:

- `Length` (Int32 little-endian)
- `Payload` (`Length` octets)

Le `Payload` commence toujours par `PacketId` (Byte).

Implementations :

- Lecture / ecriture : `TcpFrameCodec` cote Client (`Frog.Client/Network/TcpFrameCodec.cs`) ; cote Serveur traitement analogue dans la session TCP.

## Packet IDs

Valeurs partagees dans `Frog.Core/Enums/PacketId.cs`:

- `1` -> `Hello`
- `2` -> `LoginRequest`
- `3` -> `LoginResult`
- `4` -> `MapRequest`
- `5` -> `MapData`
- `6` -> `RegisterRequest`
- `7` -> `RegisterResult`
- `8` -> `MoveRequest`
- `9` -> `PositionUpdate`
- `10` -> `PlayerLeave`
- `11` -> `HeartbeatRequest`
- `12` -> `HeartbeatAck`
- `13` -> `LogoutRequest`
- `14` -> `LogoutAck`
- `15` -> `ChatSend`
- `16` -> `ChatMessage`
- `17` -> `MeleeAttackRequest`
- `18` -> `MeleeAttackResult`
- `19` -> `MapAlreadySynced`
- `20` -> `CharacterPayload`
- `21` -> `CharacterListRequest`
- `22` -> `CharacterListResult`
- `23` -> `CharacterSelectRequest`
- `24` -> `CharacterSelectResult`
- `25` -> `CharacterCreateRequest`
- `26` -> `CharacterCreateResult`
- `27` -> `CharacterStatsUpdateRequest`
- `28` -> `CharacterStatsUpdateResult`
- `29` -> `MapEventsRequest`
- `30` -> `MapEventsResult`
- `31` -> `InteractRequest`
- `32` -> `InteractResult`
- `255` -> `Error`

## Grille monde et pixels

À partir de **`FrogWireProtocol.Version` ≥ 7**, l’autorité de position est le **centre du joueur en pixels monde** (entiers) : le joueur peut se trouver **entre deux tuiles**. Les champs `PositionX` / `PositionY` (ou équivalents tuile) côté session serveur restent des **indices de tuile dérivés** (`floor(pixel / tailleTuile)`) pour les warps, événements carte et interactions sur la grille.

Constantes partagées dans `Frog.Core/Constants/WorldMetrics.cs` :

- `DefaultTileSizePixels` = **32** (carré ; aligné avec l’éditeur de cartes et le découpage `SrcX`/`SrcY` des tuiles)
- `PlayerMovePixelsPerRequest` = **8** (pas par `MoveRequest`, diagonale normalisée)
- `PlayerCollisionRadiusPixels` = **10** (disque vs tuiles `Block`)
- `PlayerMinCenterSeparationPixels` = **22** (collision joueur ↔ joueur si `AllowPlayerOverlap` désactivé sur la carte)
- `MeleeRangePixels` = **56** (distance euclidienne max. centre → centre pour un coup au corps à corps ; ~1,75 tuile)

La persistance MariaDB / `character_world_state.pos_x`, `pos_y` stocke ces **pixels centre** (anciennes données en « tuile seule » peuvent nécessiter une migration : `pos_k = pos_k * 32 + 16`).

## Carte monde (.fmap) et blob MapData

Le blob `MapBytes` du paquet **`MapData`** est **exactement** le meme contenu qu’un fichier **`.fmap`** écrit par l’éditeur (`Frog.Core/IO/MapSerializer.cs`) — meme suite d’octets.

### Séquence fichier / blob

Après les 4 octets magic ASCII `FMAP` :

1. **`MapFileFormatVersion`** (`byte`), exposé dans le code sous `MapSerializer.MapFileFormatVersion` (aligné avec l’élément compilé dans le dépôt ; **valeur attendue : 4**, avec compatibilité de **lecture** pour les blobs **v3** déjà distribués).
2. `Width`, `Height` (`Int32` LE chaque).
3. Nom de carte : longueur `Int32` + UTF‑8…
4. **à partir du format v4 uniquement :** octet d’options (bit 0 = « chevauchement joueurs autorisé », flag carte `AllowPlayerOverlap` côté `Frog.Core.Models.Map`).
5. Par la suite : `LayerCount` puis couches (voir `MapSerializer` pour tous les détails par couche).

Les fichiers `.fmap` **v3** n’avaient pas l’octet d’options : la valeur par défaut côté modèle après désérialisation est « overlap désactivé ».

Le serveur charge la carte **primaire** (identifiant de session monde par défaut `MapService.DefaultWorldMapId`) depuis **`Maps:worldMapPath`** puis, si nécessaire, **`frog_map`** (`DatabaseFallbackMapId`). Les **autres cartes** ne sont téléchargées qu’après téléchargement des blobs **`frog_map`** correspondants : lorsqu’un joueur doit déjà être sur une autre carte (persistance joueur ou warp réussi vers un `frog_map.id` disponible).

Chemin fichier vide ou illisible → carte de secours intégrée (puis autres cartes via MariaDB comme ci‑dessus).

Guide utilisateur pas à pas : [`Docs/premier-monde.md`](../../Docs/premier-monde.md).

### Manifeste tilesets (export éditeur)

À l’enregistrement d’un fichier `MaCarte.fmap`, l’éditeur écrit **`MaCarte.tilesets.json`** (UTF‑8 JSON) listant les paires `{ id, fileName }` (`fileName` = nom seul du PNG). Le client résout les chemins relativement au dossier du fichier manifest (voir guide `premier-monde.md`).

## Messages

### Hello (Serveur -> Client)

Premier paquet après accept TCP. Construction : `Frog.Core/Protocol/WireHello.cs` (`WireHello.BuildPayload`).

Payload :

- `PacketId` (Byte) = `1`
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets) — défaut **`FROG SERVER READY`** (`WireHello.DefaultMessage`)
- **`ProtocolVersion`** (`UInt16` LE) — constante **`FrogWireProtocol.Version`** (`Frog.Core/Constants/FrogWireProtocol.cs`)

Sans ces 2 octets finaux, le client Frog actuel doit refuser (`Hello serveur incomplet ou obsolète`).

### LoginRequest (Client -> Serveur)

Payload:

- `PacketId` (Byte) = `2`
- `UsernameLength` (Byte)
- `UsernameUtf8` (`UsernameLength` octets)
- `PasswordLength` (Byte)
- `PasswordUtf8` (`PasswordLength` octets)

Exemple de compte bootstrap: `demo/demo`.

### RegisterRequest (Client -> Serveur)

Payload:

- `PacketId` (Byte) = `6`
- `UsernameLength` (Byte)
- `UsernameUtf8` (`UsernameLength` octets)
- `PasswordLength` (Byte)
- `PasswordUtf8` (`PasswordLength` octets)

### LoginResult (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `3`
- `Success` (Byte, `1` ou `0`)
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets)

Immédiatement après un `LoginResult` **réussi**, le serveur peut envoyer **`CharacterPayload`** (`FrogWireProtocol.Version` **≥ 3**) avec le JSON DB du perso courant (perso actif après bootstrap, ex. **Hero**).

À partir de **`FrogWireProtocol.Version` ≥ 4**, le client peut demander la **liste des personnages** du compte (`CharacterListRequest` / `CharacterListResult`) puis activer un autre slot (`CharacterSelectRequest` / `CharacterSelectResult`) ; le serveur renvoie alors un **`CharacterPayload`** pour le nouvel UUID et diffuse des **`PositionUpdate`** à tous les clients connectés.

À partir de **`FrogWireProtocol.Version` ≥ 5**, le client peut **créer** un perso additionnel (`CharacterCreateRequest` / `CharacterCreateResult`) : nom affichage validé côté serveur (lettres/chiffres/espaces/tiret/souligné, longueur max 32), max **8** persos par compte, payload stats par défaut comme **Hero**.

À partir de **`FrogWireProtocol.Version` ≥ 6**, le client peut mettre à jour les **six stats** du perso actif (`CharacterStatsUpdateRequest` / `CharacterStatsUpdateResult`) : le corps est **6 octets** dans l’ordre **STR, AGI, DEX, INT, VIT, LUCK**, chaque octet entre **1** et **99**. En cas de succès, le serveur persiste dans `frog_character.payload` (objet JSON `stats`) et peut renvoyer un **`CharacterPayload`** à jour.

À partir de **`FrogWireProtocol.Version` ≥ 7**, les coordonnées **`PositionUpdate`** et la persistance monde sont en **pixels centre** (voir section *Grille monde et pixels*).

Additive **après wire v6** : **`MapEventsRequest`** (**29**) corps **vide** après l’opcode (session authentifiée). Le serveur répond **`MapEventsResult`** (**30**) avec **`CurrentMapId`**, puis longueur **UInt16 LE** puis JSON UTF‑8 : tableau d’objets `MapEventWireEntry` (`placementId`, `catalogId`, `slug`, `displayName`, `tileX`, `tileY`, **`triggerKind`**). **`triggerKind`** vaut `interact` (défaut si absent du JSON), `step_on` (à l’**arrivée** sur la tuile) ou **`page`** (une fois par **entrée sur la carte** sur la tuile d’arrivée, voir ci‑dessous). Si MariaDB est désactivée ou sans lignes, la réponse est un tableau vide. Le client peut renvoyer cette requête après **`MapData`** / **`MapAlreadySynced`** ou un changement de carte.

**`InteractRequest`** (**31**), corps vide : interaction sur la **tuile courante** du joueur (`PositionX` / `PositionY` grille). Réponse **`InteractResult`** (**32**), même forme que **`LoginResult`**. Seuls les placements dont **`triggerKind`** est **`interact`** sont pris en compte : s’il en existe au moins un sur cette tuile, **succès** avec message `"{displayName} ({slug})"` pour l’entrée **la plus petite** (`catalogId`, puis `placementId`) ; sinon échec « Rien a interagir ici. ».

Placements **`step_on`** : après un **`MoveRequest`** ou **`PositionSyncRequest`** réussi **et** si le couple **(carte, tuile)** du joueur a **changé** par rapport à l’état avant la requête (mouvement ou warp inclus), le serveur peut envoyer au client concerné un **`InteractResult`** réussi avec le message préfixé **`[Marche]`** (même tri `catalogId`, puis `placementId` sur les `step_on` de la tuile d’arrivée). Ainsi pas de spam si le client renvoie la même position sans changer de case.

Placements **`page`** : quand **`CurrentMapId`** change (connexion, sélection de perso avec autre carte, warp), après positionnement sur la nouvelle carte le serveur peut envoyer un **`InteractResult`** réussi avec **`[Page]`** pour au plus un placement `page` sur la **tuile courante**, **une fois par visite** de cette carte (réarmé en quittant la carte). Si aucun `page` sur la tuile d’arrivée, aucun message ; la visite est tout de même marquée pour ne pas répéter.

Côté serveur MariaDB, les placements sont mis en cache par carte ; l’empreinte de cache inclut notamment `COUNT(*)`, `MAX(id)` et un agrégat sur le contenu des lignes (dont `trigger_kind`) pour refléter les mises à jour sans redémarrage.

### RegisterResult (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `7`
- `Success` (Byte, `1` ou `0`)
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets)

### MapRequest (Client -> Serveur)

Charge utile :

- `PacketId` (Byte) = `4`
- **Optionnel (**`FrogWireProtocol.Version` **≥ 2**, inchangé en v3) :** `FingerprintRevision` (`Int64` LE) puis `FingerprintSha256` (**32 octets**, identique au hash utilisé dans `MapData` / réponse `frog_map`)

Si le corps après l’opcode est vide, le serveur renvoie le blob complet **de la carte courante de la session** (`Session.CurrentMapId`) avec empreinte.  
Si une empreinte de **40 octets** est envoyée **et** qu’elle correspond exactement à **cette** carte chargée, le serveur peut répondre par **`MapAlreadySynced`** sans renvoyer le blob.

Une longueur de payload autre que `0` ou `40` est **erreur**.

Note : une session **authentifiée** reste obligatoire.

Après un **warp** (changement de carte côté serveur), le joueur local reçoit un `PositionUpdate` dont le `MapId` reflète la carte courante de la session. Le client graphique doit continuer à appliquer `x,y` pour soi même si l’UI affiche encore l’ancienne carte, puis déclencher un `MapRequest` (corps vide si aucune empreinte n’est encore connue pour cette carte) pour recevoir `MapData` / `MapAlreadySynced` et rafraîchir l’affichage — en pratique avec un léger debounce pour éviter les rafales réseau.

### MapData (Serveur -> Client)

Payload :

- `PacketId` (Byte) = `5`
- `MapId` (Int32 little-endian)
- `MapLength` (Int32 little-endian)
- `MapBytes` (`MapLength` octets)
- **(`FrogWireProtocol.Version` ≥ 2)** `FingerprintRevision` (`Int64` LE)
- puis `FingerprintSha256` (**32 octets**).

`MapBytes` est toujours le fichier `.fmap` complet pour ce `MapId` : désérialiser uniquement cette tranche avec `MapSerializer`. Le client doit conserver **`FingerprintRevision`** + **`FingerprintSha256`** pour pouvoir envoyer un `MapRequest` « HEAD » suivant.


### MapAlreadySynced (Serveur -> Client)

Réponse lorsque la demande carte porte déjà les bons métadonnées.

Payload :

- `PacketId` (Byte) = `19`
- `MapId` (`Int32` LE)
- `FingerprintRevision` (`Int64` LE)
- `FingerprintSha256` (**32 octets**)

Pas de blob carte. Le client met à jour son cache d’empreinte pour les requêtes suivantes.

### MeleeAttackRequest (Client -> Serveur)

Requiert une session authentifiee. Cible identifiee par **nom d'utilisateur** (meme convention longueur que le login).

Payload :

- `PacketId` (Byte) = `17`
- `TargetUsernameLength` (Byte, > 0, max comme login)
- `TargetUsernameUtf8` (`TargetUsernameLength` octets)

Le serveur verifie que la cible est en ligne, sur la **meme carte**, et a une distance centre–centre <= `MeleeRangePixels` (voir section Grille). Il envoie un `MeleeAttackResult` a l'attaquant ; si touche, un second `MeleeAttackResult` a la victime.

### MeleeAttackResult (Serveur -> Client)

Payload :

- `PacketId` (Byte) = `18`
- `Hit` (Byte, `1` = touche, `0` = rate / refuse)
- `TargetUsernameLength` (Byte)
- `TargetUsernameUtf8` (`TargetUsernameLength` octets) — pour l'attaquant : nom de la cible ; pour la victime : nom de l'attaquant dans le cas envoye au defenseur
- `MessageLength` (UInt16 little-endian)
- `MessageUtf8` (`MessageLength` octets), texte explicatif court (ex. "Touche.", "Hors portee.", "Cible hors ligne.")

### MoveRequest (Client -> Serveur)

Payload:

- `PacketId` (Byte) = `8`
- `DeltaX` (Int8 signe)
- `DeltaY` (Int8 signe)

Regles serveur:

- pas de mouvement `(0,0)`
- chaque delta doit etre dans `[-1, 1]`
- le mouvement doit rester dans les limites de map
- depuis la **v7** du protocole, le mouvement applique un pas en **pixels** (voir `WorldMetrics.PlayerMovePixelsPerRequest`) avec collision **cercle joueur ↔ tuiles bloquées** (`PlayerCollisionRadiusPixels`)
- si la carte **n’a pas** le flag `AllowPlayerOverlap`, le pas est refusé lorsque le **centre** projeté serait à moins de `PlayerMinCenterSeparationPixels` du centre d’un autre joueur sur la même carte

**Warps** : après un mouvement réussi, si la case d’arrivée est une tuile **Warp** (`TileType.Warp`), le serveur téléporte vers la **carte cible** (`WarpTargetMapId`, `0` = carte monde par défaut) si le blob `frog_map` pour cette cible est **chargé** (présent en base et désérialisable). Sinon le joueur reste sur la case warp. La case d’arrivée doit être libre (pas bloc ; même règle **joueur** que pour les pas si `AllowPlayerOverlap` est absent sur la carte **d’arrivée**).

### PositionUpdate (Serveur -> Clients authentifies)

Payload:

- `PacketId` (Byte) = `9`
- `UsernameLength` (Byte)
- `UsernameUtf8` (`UsernameLength` octets)
- **`MapId` (Int32 LE)** — carte logique du joueur (`FrogWireProtocol.Version` **≥ 3** ; clients obsolètes ne peuvent pas parser ce flux)
- **`PixelCenterX` / `PixelCenterY` (Int32 LE chaque)** — depuis la version **7** : centre joueur en pixels monde (axes 0…`Width*TailleTuile-1`). Versions **&lt; 7** du client : anciennement **indices de tuile** au même emplacement.

Les clients doivent **ignorer** les mises à jour dont le `MapId` ne correspond pas à la carte actuellement affichée (sinon superposition d’AOI entre cartes).

### CharacterPayload (Serveur -> Client)

Payload (protocole **≥ 3**) :

- `PacketId` (Byte) = `20`
- `CharacterIdUtf8Length` (Byte, > 0, même borne pratique que login)
- `CharacterIdUtf8` (longueur ci‑dessus)
- `JsonLength` (`UInt16` LE)
- `JsonUtf8` (`JsonLength` octets) — contenu typique : `frog_character.payload` (ex. stats JSON)

Envoyé après login réussi lorsque le lecteur perso connaît l’UUID (`Session.CharacterId`), et à nouveau après un **`CharacterSelectRequest`** réussi.

### CharacterListRequest (Client -> Serveur)

`FrogWireProtocol.Version` **≥ 4**. Session authentifiée.

Payload : uniquement `PacketId` (Byte) = `21` (corps vide après l’opcode dans la frame).

### CharacterListResult (Serveur -> Client)

Payload :

- `PacketId` (Byte) = `22`
- `JsonLength` (`UInt16` LE)
- `JsonUtf8` (`JsonLength` octets) — tableau JSON d’objets `{ "id": "<uuid frog_character>", "name": "<display_name>" }` (voir `Frog.Core.Protocol.CharacterListWireEntry`).

### CharacterSelectRequest (Client -> Serveur)

`FrogWireProtocol.Version` **≥ 4**. Session authentifiée.

Payload :

- `PacketId` (Byte) = `23`
- `CharacterIdUtf8Length` (Byte, > 0, max **64** comme borne pratique login / `ChatProtocolLimits.MaxUsernameUtf8Bytes`)
- `CharacterIdUtf8` (longueur ci‑dessus) — UUID texte `frog_character.id`

Le serveur sauvegarde la position du perso **précédent**, charge carte / tuiles pour le perso choisi (`character_world_state` ou défaut monde), puis répond **`CharacterSelectResult`** et **`CharacterPayload`**.

### CharacterSelectResult (Serveur -> Client)

Même forme que **`LoginResult`** :

- `PacketId` (Byte) = `24`
- `Success` (Byte)
- `MessageLength` (Byte)
- `MessageUtf8`

### CharacterCreateRequest (Client -> Serveur)

`FrogWireProtocol.Version` **≥ 5**. Session authentifiée.

Payload :

- `PacketId` (Byte) = `25`
- `DisplayNameUtf8Length` (Byte, > 0, max **128** octets UTF‑8 ; côté serveur le nom affichage est limité à **32** caractères après trim)
- `DisplayNameUtf8` (longueur ci‑dessus)

### CharacterCreateResult (Serveur -> Client)

Même forme que **`LoginResult`** :

- `PacketId` (Byte) = `26`
- `Success` (Byte)
- `MessageLength` (Byte)
- `MessageUtf8` — en cas de succès : **UUID** texte du nouveau `frog_character.id` (≤ 36 caractères ASCII)

### CharacterStatsUpdateRequest (Client -> Serveur)

`FrogWireProtocol.Version` **≥ 6**. Session authentifiée, personnage actif.

Payload :

- `PacketId` (Byte) = `27`
- **6 octets** : STR, AGI, DEX, INT, VIT, LUCK — chaque octet dans **1**…**99**

### CharacterStatsUpdateResult (Serveur -> Client)

Même forme que **`LoginResult`** :

- `PacketId` (Byte) = `28`
- `Success` (Byte)
- `MessageLength` (Byte)
- `MessageUtf8`

### MapEventsRequest (Client -> Serveur)

Additive post **v6**. Session authentifiée.

Payload :

- `PacketId` (Byte) = `29`
- **aucun octet** suivant (corps vide après l’opcode dans la frame).

### MapEventsResult (Serveur -> Client)

- `PacketId` (Byte) = `30`
- `MapId` (**Int32** LE) — valeur `Session.CurrentMapId` au moment de la requête
- `JsonUtf8Length` (**UInt16** LE)
- `JsonUtf8` — tableau JSON d’objets `MapEventWireEntry` (`Frog.Core/Protocol/MapEventsWire.cs`) — champs notamment `placementId`, `catalogId`, `slug`, `displayName`, `tileX`, `tileY`, `triggerKind` (`interact`, `step_on` ou `page`).

**Client (`MapViewRenderer`)** : sur la vue carte monde, surbrillance **rectangle** pour **`interact`**, **losange** pour **`step_on`**, **cercle** (coin bas-gauche de la tuile) pour **`page`** (couleurs d’accent inchangées par slug, ex. démo).

### InteractRequest (Client -> Serveur)

Session authentifiée.

Payload :

- `PacketId` (Byte) = `31`
- **aucun octet** suivant.

### InteractResult (Serveur -> Client)

Même forme que **`LoginResult`** :

- `PacketId` (Byte) = `32`
- `Success` (Byte)
- `MessageLength` (Byte)
- `MessageUtf8`

### PlayerLeave (Serveur -> Clients authentifies)

Envoye quand un joueur quitte (deconnexion TCP ou session expiree par inactivite).

Payload:

- `PacketId` (Byte) = `10`
- `UsernameLength` (Byte)
- `UsernameUtf8` (`UsernameLength` octets)

### HeartbeatRequest (Client -> Serveur)

Maintient la session active (anti-timeout d'inactivite). Requiert une session authentifiee.

Payload:

- `PacketId` (Byte) = `11`

### HeartbeatAck (Serveur -> Client)

Reponse minimale au heartbeat.

Payload:

- `PacketId` (Byte) = `12`

### LogoutRequest (Client -> Serveur)

Deconnexion volontaire: retire la session, notifie les autres via `PlayerLeave`, envoie `LogoutAck`, puis ferme la connexion TCP cote serveur.

Payload:

- `PacketId` (Byte) = `13`

### LogoutAck (Serveur -> Client)

Confirme que la deconnexion serveur est terminee.

Payload:

- `PacketId` (Byte) = `14`

### ChatSend (Client -> Serveur)

Requiert une session authentifiee. Canaux (`ChatChannel` dans `Frog.Core/Enums/ChatChannel.cs`) :

- `0` = Global — tous les joueurs connectes
- `1` = Map — joueurs sur la meme `CurrentMapId` que l'emetteur
- `2` = Whisper — message au joueur cible uniquement (+ echo a l'emetteur)

Corps du paquet (octets apres le `PacketId` `15` dans la frame, comme pour les autres paquets client) :

- `Channel` (Byte)
- Si `Channel == 2` (Whisper) : `TargetUsernameLength` (Byte), `TargetUsernameUtf8`
- `MessageLength` (UInt16 little-endian)
- `MessageUtf8` (`MessageLength` octets, max 512 octets UTF-8)

### ChatMessage (Serveur -> Client)

Payload :

- `PacketId` (Byte) = `16`
- `Channel` (Byte) — meme encodage que `ChatSend`
- `FromUsernameLength` (Byte)
- `FromUsernameUtf8`
- `ToUsernameLength` (Byte) — `0` si global/map
- `ToUsernameUtf8` (optionnel)
- `MessageLength` (UInt16 little-endian)
- `MessageUtf8`

### Error (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `255`
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets)

## Sequence recommandee cote client

1. Se connecter en TCP.
2. Lire une frame puis `Hello` ; vérifier **`ProtocolVersion` == client** ; sinon deconnecter.
3. Si besoin, envoyer `RegisterRequest`.
4. Attendre `RegisterResult`.
5. Envoyer `LoginRequest`.
6. Attendre `LoginResult` et verifier `Success == 1`.
7. Envoyer `MapRequest`.
8. Attendre `MapData`, deserialiser `MapBytes`.
9. Envoyer `MoveRequest` au fil des entrees joueur.
10. Traiter les `PositionUpdate` recus (soi + autres joueurs connectes).
11. Retirer l'entite locale quand un `PlayerLeave` est recu.
12. Envoyer periodiquement `HeartbeatRequest` si le joueur reste immobile longtemps (inferieur au timeout serveur).
13. Envoyer `ChatSend` et traiter les `ChatMessage` recus (global / map / whisper).

Note: apres un `LoginResult` reussi, le serveur peut immediatement envoyer des `PositionUpdate`
pour fournir l'etat initial des joueurs deja connectes.

Note: le serveur restaure `PositionX` / `PositionY` / `CurrentMapId` depuis la derniere sauvegarde si disponible (MariaDB ou memoire selon configuration).
