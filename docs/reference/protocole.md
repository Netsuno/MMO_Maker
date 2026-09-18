# Référence — Protocole filaire (v10)

**Version wire actuelle :** `FrogWireProtocol.Version = 10` (`Frog.Core/Constants/FrogWireProtocol.cs`).
**Source opcodes :** `Frog.Core/Enums/PacketId.cs`.

> Fiches progressives. Détail binaire exact = code + tests ; ici : rôle + entrées/sorties utiles aux auteurs de clients / outils.

## Hello (1)

| | |
| --- | --- |
| **Rôle** | Annonce serveur + version protocole |
| **Entrées** | (S→C) message UTF-8 + version u16 |
| **Sorties** | Client aligne sa version attendue |
| **Erreurs** | Incompatibilité de version → pas de session utile |
| **État** | Livré |

## LoginRequest / LoginResult (2 / 3)

| | |
| --- | --- |
| **Rôle** | Authentifier un compte |
| **Entrées** | Identifiants (formes wire : voir codecs login) |
| **Sorties** | Succès/échec + message court ; session si OK |
| **Erreurs** | Identifiants invalides ; rate-limit (clé IP:port — limite connue Phase 9) |
| **État** | Livré |

## RegisterRequest / RegisterResult (6 / 7)

| | |
| --- | --- |
| **Rôle** | Créer un compte |
| **Entrées** | Données d’inscription |
| **Sorties** | Succès/échec |
| **Erreurs** | Nom pris / validation |
| **État** | Livré — **bêta fermée absente** (pas d’invitation) ; à resserrer P10-5 |

## CharacterList / Select / Create (21–26)

| | |
| --- | --- |
| **Rôle** | Lister, choisir ou créer un personnage |
| **Entrées** | Session authentifiée ; nom / id selon opcode |
| **Sorties** | Liste JSON `{ id, name }` ; résultats courts type LoginResult |
| **Erreurs** | Session absente ; nom invalide |
| **État** | Livré |

## MoveRequest / PositionSyncRequest / PositionUpdate (8 / 33 / 9)

| | |
| --- | --- |
| **Rôle** | Déplacement ; sync pixel validée serveur |
| **Entrées** | Direction ou centre pixel (Int32×2) |
| **Sorties** | `PositionUpdate` diffusé si valide |
| **Erreurs** | Vitesse / collisions refusées (pas de téléport client) |
| **État** | Livré |

## InteractRequest / InteractResult (31 / 32)

| | |
| --- | --- |
| **Rôle** | Interaction tuile courante (événements carte) |
| **Entrées** | Guid `activationId` (v10) |
| **Sorties** | Résultat corrélé + effets événement |
| **Erreurs** | Activation invalide / hors portée |
| **État** | Livré |

## ChatSend / ChatMessage (15 / 16)

| | |
| --- | --- |
| **Rôle** | Envoyer / recevoir du chat |
| **Entrées** | Canal + texte ; canaux v10 : Global 0 / Map 1 / Whisper 2 **uniquement** |
| **Sorties** | `ChatMessage` aux destinataires |
| **Erreurs** | Canal autre que 0–2 **rejeté** ; mute |
| **État** | Livré — Party/Guild = v11 (voir social) |

## ModerateRequest / ModerateResult (78 / 79)

| | |
| --- | --- |
| **Rôle** | Mute / kick / ban (opérateur) |
| **Entrées** | Cible + action (voir P9-1) |
| **Sorties** | Résultat + effet session |
| **Erreurs** | Pas opérateur ; cible invalide |
| **État** | Livré Phase 9 |

## WorldFlagsPatchRequest (34)

| | |
| --- | --- |
| **Rôle** | Patch drapeaux monde côté wire |
| **Entrées** | Payload JSON booléens |
| **Sorties** | — |
| **Erreurs** | **Rejeté** en production PostgreSQL |
| **État** | Rejet volontaire (sécurité) |

## Inventaire / économie (38–59) — aperçu

Opcodes livrés : `InventorySnapshot`, equip/unequip, drop/pickup, shop buy/sell, bank deposit/withdraw/snapshot, combat/spell/respawn, catalogues publiés, dialogue 66–68, quêtes 69–71, craft (suite enum).
Fiches détaillées : [actions-joueur.md](actions-joueur.md).
