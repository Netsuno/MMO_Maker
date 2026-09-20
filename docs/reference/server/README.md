# Référence — Server

← [Référence](../README.md)

Fonctions Server, A–Z (1 fichier / fonction). Tip miroir : `d6e59759` (#31 · #32 EconomyHub · #33 InstanceHub).

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [isblocked](isblocked.md) | `isblocked(mapId, x, y)` | Tuile bloquante ? |
| [loadpublishedworld](loadpublishedworld.md) | `loadpublishedworld(maps, catalog)` | Charge le monde publié |
| [moderationexecuteasync](moderationexecuteasync.md) | `moderationexecuteasync(actorAccountId, action, targetUsername, reason)` | Mute/kick/ban |
| [registeraccountasync](registeraccountasync.md) | `registeraccountasync(username, password)` | Crée un compte |
| [registerreconnectfailure](registerreconnectfailure.md) | `registerreconnectfailure(rateLimitKey)` | Échec reconnect (rate) |
| [registerreconnectsuccess](registerreconnectsuccess.md) | `registerreconnectsuccess(rateLimitKey)` | Succès reconnect (rate) |
| [tryallowreconnect](tryallowreconnect.md) | `tryallowreconnect(rateLimitKey)` | Autorise une tentative reconnect |
| [tryapplymove](tryapplymove.md) | `tryapplymove(session, deltaX, deltaY)` | Applique un pas grille |
| [tryapplyreportedpixelposition](tryapplyreportedpixelposition.md) | `tryapplyreportedpixelposition(session, x, y)` | Valide sync pixel |
| [tryauthenticateasync](tryauthenticateasync.md) | `tryauthenticateasync(username, password, rateLimitKey)` | Auth compte |
| [trygetwarpdestination](trygetwarpdestination.md) | `trygetwarpdestination(mapId, tileX, tileY)` | Destination warp |
| [tryteleporttotile](tryteleporttotile.md) | `tryteleporttotile(session, targetMapId, tileX, tileY)` | Téléporte sur tuile |
| [setenabledoverride](setenabledoverride.md) | `setenabledoverride(enabled?)` | Override maintenance |
| [shouldrejectloginasync](shouldrejectloginasync.md) | `shouldrejectloginasync(accountId?)` | Refus login maintenance |

## EconomyHub / InstanceHub (scaffolding)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [economyhubservice-executeasync](economyhubservice-executeasync.md) | `executeasync(session, kind, action, requestId, extra, ct)` | Query-only in-memory |
| [instancehubservice-execute](instancehubservice-execute.md) | `execute(session, kind, action, requestId, extra)` | Query/Enter/Leave in-memory |
