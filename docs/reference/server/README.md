# Référence — Server

← [Référence](../README.md)

Fonctions Server, A–Z (1 fichier / fonction). Tip miroir : `2af2dd22` (#31 · #32 EconomyHub · #33 InstanceHub).

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [isblocked](isblocked.md) | `isblocked(mapId, x, y)` | Tuile bloquante ? |
| [loadpublishedworld](loadpublishedworld.md) | `loadpublishedworld(maps, catalog)` | Charge le monde publié |
| [moderationexecuteasync](moderationexecuteasync.md) | `moderationexecuteasync(actorAccountId, action, targetUsername, reason)` | Mute/kick/ban |
| [registeraccountasync](registeraccountasync.md) | `registeraccountasync(username, password)` | Crée un compte |
| [registerreconnectfailure](registerreconnectfailure.md) | `registerreconnectfailure(remoteEndPoint)` | Échec reconnect (rate) |
| [registerreconnectsuccess](registerreconnectsuccess.md) | `registerreconnectsuccess(remoteEndPoint)` | Succès reconnect (rate) |
| [tryallowreconnect](tryallowreconnect.md) | `tryallowreconnect(remoteEndPoint)` | Autorise une tentative reconnect |
| [tryapplymove](tryapplymove.md) | `tryapplymove(session, deltaX, deltaY)` | Applique un pas grille |
| [tryapplyreportedpixelposition](tryapplyreportedpixelposition.md) | `tryapplyreportedpixelposition(session, reportedPixelX, reportedPixelY)` | Valide sync pixel |
| [tryauthenticateasync](tryauthenticateasync.md) | `tryauthenticateasync(username, password, remoteEndPoint)` | Auth compte |
| [trygetwarpdestination](trygetwarpdestination.md) | `trygetwarpdestination(mapId, tileX, tileY)` | Destination warp |
| [tryteleporttotile](tryteleporttotile.md) | `tryteleporttotile(session, targetMapId, tileX, tileY)` | Téléporte sur tuile |
| [setenabledoverride](setenabledoverride.md) | `setenabledoverride(enabled?)` | Override maintenance |
| [shouldrejectloginasync](shouldrejectloginasync.md) | `shouldrejectloginasync(accountId?)` | Refus login maintenance |

## EconomyHub / InstanceHub (scaffolding)

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [economyhubservice-executeasync](economyhubservice-executeasync.md) | `executeasync(session, kind, action, requestId, extra, ct)` | Query-only in-memory |
| [instancehubservice-execute](instancehubservice-execute.md) | `execute(session, kind, action, requestId, extra)` | Query/Enter/Leave in-memory |

## Zones publiées (#147) et CTS1 (#148)

Réapparition des zones publiées (#147) et GET CTS1 (#148), A–Z. `ExecuteAsync` du service hébergé est `protected`. `MapPersistenceMapper` est `internal`. Pipeline : [tuiles 48×48](../pipeline-tuiles-48.md).

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [abort](abort.md) | `abort(reservationId) → void` | Libère une réservation dont le spawn a échoué ou quitté la zone |
| [applyasync](applyasync.md) | `applyasync(keeper, zones, aliveInstanceIds, utcNow, spawn, cancellationToken?) → int` | Pose les ordres du passage et compte ceux confirmés |
| [commit](commit.md) | `commit(reservationId, instanceId) → void` | Attache l’instance vivante à la réservation |
| [composedtilesetclientpath](composedtilesetclientpath.md) | `composedtilesetclientpath → string` | Expose le GET de la charge CTS1 sur le canal HTTP de contenu |
| [listmobspawnzonesasync](listmobspawnzonesasync.md) | `listmobspawnzonesasync(cancellationToken?) → IReadOnlyList<PublishedMobSpawnZone>` | Lit les zones de monstres du snapshot publié, pas du brouillon |
| [mobspawnorder](mobspawnorder.md) | `mobspawnorder(reservationId, mapId, runtimeMapId, zoneId, entryId, monsterId, tileX, tileY) → MobSpawnOrder` | Ordre d’apparition réalisé par SpawnMonster, pas un second type de monstre |
| [mobspawnzonehostedservice](mobspawnzonehostedservice.md) | `interval → TimeSpan` | Service d’arrière-plan qui réapparaît les monstres des zones publiées |
| [publishedmobspawnzone](publishedmobspawnzone.md) | `publishedmobspawnzone(mapId, runtimeMapId, zone) → PublishedMobSpawnZone` | Zone publiée reliée à l’identifiant runtime des monstres |
| [tick](tick.md) | `tick(zones, aliveInstanceIds, utcNow) → IReadOnlyList<MobSpawnOrder>` | Calcule les apparitions dues et arme le délai à la mort observée |
| [trycreatesignedpackasync](trycreatesignedpackasync.md) | `trycreatesignedpackasync(cancellationToken?) → byte[]?` | Assemble la charge CTS1 signée des tilesets composés publiés |
