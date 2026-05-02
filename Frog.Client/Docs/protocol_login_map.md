# Protocole Sprint 2: Login + Map

Ce document decrit le protocole reseau minimal utilise entre le client et le serveur pour le flux:

1. connexion TCP
2. login
3. demande de map
4. reception de map

Le transport est base sur des *frames* binaires.

## Encapsulation des frames

Chaque message reseau est encode ainsi:

- `Length` (Int32 little-endian)
- `Payload` (`Length` octets)

Le `Payload` commence toujours par `PacketId` (Byte).

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
- `255` -> `Error`

## Messages

### Hello (Serveur -> Client)

Payload:

- `PacketId` (Byte) = `1`
- `MessageLength` (Byte)
- `MessageUtf8` (`MessageLength` octets)

Message actuel: `FROG SERVER READY`.

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

`MapBytes` est le blob serialise par `Frog.Core/IO/MapSerializer.cs`.

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
2. Lire `Hello`.
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
