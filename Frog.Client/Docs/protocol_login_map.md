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
- `255` -> `Error`

## Grille monde et pixels

Le serveur aligne la position joueur sur une grille **tuiles** (voir `PositionUpdate`) et maintient en interne le **centre** de la tuile en **pixels monde** pour la mêlée. Constantes partagées dans `Frog.Core/Constants/WorldMetrics.cs` :

- `DefaultTileSizePixels` = **32** (carré ; aligné avec l’éditeur de cartes et le découpage `SrcX`/`SrcY` des tuiles)
- `MeleeRangePixels` = **56** (distance euclidienne max. centre → centre pour un coup au corps à corps ; ~1,75 tuile)

## Carte monde (.fmap) et blob MapData

Le blob `MapBytes` du paquet **`MapData`** est **exactement** le meme contenu qu’un fichier **`.fmap`** écrit par l’éditeur (`Frog.Core/IO/MapSerializer.cs`) — meme suite d’octets.

### Séquence fichier / blob

Après les 4 octets magic ASCII `FMAP` :

1. **`MapFileFormatVersion`** (`byte`), exposé dans le code sous `MapSerializer.MapFileFormatVersion` (aligné avec l’élément compilé dans le dépôt).
2. `Width`, `Height` (`Int32` LE chaque).
3. Nom de carte : longueur `Int32` + UTF‑8… (voir implémentation `MapSerializer` pour tous les détails par couche).

Le serveur charge ce format depuis **`Maps:worldMapPath`** (`Frog.Server/appsettings.json`). Chemin vide ou fichier illisible → carte de secours intégrée.

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

### RegisterResult (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `7`
- `Success` (Byte, `1` ou `0`)
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets)

### MapRequest (Client -> Serveur)

Payload:

- `PacketId` (Byte) = `4`

Note: le serveur exige une session authentifiee.

### MapData (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `5`
- `MapId` (Int32 little-endian)
- `MapLength` (Int32 little-endian)
- `MapBytes` (`MapLength` octets)

`MapBytes` est le fichier `.fmap` complet (magic `FMAP` + `MapFileFormatVersion` + reste) : voir **Carte monde (.fmap) et blob MapData** ci-dessus ; désérialiser avec `MapSerializer`.

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
- le mouvement est refuse sur les tuiles bloquees (`Block` / collision serveur)
- le mouvement est refuse si un autre joueur occupe deja la case cible.

**Warps (serveur, phase 1)** : apres un mouvement reussi, si la case d'arrivee est une tuile **Warp** sur la carte monde (`TileType.Warp` avec cible), le serveur peut **teleporter** le joueur dans la meme carte (`WarpTargetMapId` = monde ou `0`). Une seconde position est alors diffusee via `PositionUpdate` (meme paquet qu'apres un pas). Les warps vers une **autre** carte sont ignores tant que le systeme d'instances / multi-maps n'est pas en place. La case d'arrivee doit etre libre (pas bloc, pas autre joueur).

### PositionUpdate (Serveur -> Clients authentifies)

Payload:

- `PacketId` (Byte) = `9`
- `UsernameLength` (Byte)
- `UsernameUtf8` (`UsernameLength` octets)
- `PositionX` (Int32 little-endian)
- `PositionY` (Int32 little-endian)

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

Note: le serveur restaure `PositionX` / `PositionY` / `CurrentMapId` depuis la derniere sauvegarde si disponible (PostgreSQL ou memoire selon configuration).
