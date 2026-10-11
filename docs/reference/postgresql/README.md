# Référence — PostgreSQL

← [Référence](../README.md)

Repositories PG, A–Z (1 fichier / fonction). Tip : `2af2dd22`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [accountfindbyusernameasync](accountfindbyusernameasync.md) | `accountfindbyusernameasync(username)` | Cherche un compte |
| [accounttrycreateasync](accounttrycreateasync.md) | `accounttrycreateasync(username, password)` | Crée un compte |
| [charactercreateasync](charactercreateasync.md) | `charactercreateasync(accountId, displayName, classId, stats, maxHp, maxMp, startingSpellId, mapId, pixelX, pixelY)` | Crée un personnage |
| [characterlistbyaccountasync](characterlistbyaccountasync.md) | `characterlistbyaccountasync(accountId)` | Liste les persos |
| [charactersaveasync](charactersaveasync.md) | `charactersaveasync(character)` | Sauve un perso |
| [inventorygetasync](inventorygetasync.md) | `inventorygetasync(characterId)` | Snapshot inventaire |
| [inventorytryaddasync](inventorytryaddasync.md) | `inventorytryaddasync(characterId, itemId, quantity, maxStack)` | Ajoute des items |
| [inventorytryremoveasync](inventorytryremoveasync.md) | `inventorytryremoveasync(characterId, slotIndex, quantity)` | Retire des items |
| [maploadbyidasync](maploadbyidasync.md) | `maploadbyidasync(mapId)` | Charge une carte stockée |
| [maploadpublishedbyidasync](maploadpublishedbyidasync.md) | `maploadpublishedbyidasync(mapId)` | Charge révision publiée |
| [mapsaveasync](mapsaveasync.md) | `mapsaveasync(request)` | Sauve / publie carte |
| [questtryturninasync](questtryturninasync.md) | `questtryturninasync(characterId, questId, requestId)` | Turn-in quête transactionnel |

## Objets de carte, tuiles composées, zones JSON (#146–#148)

Dépôts objets de carte et tuiles composées, A–Z. La fiche du port couvre `Postgres*` et `InMemory*` (même signature). Six `DbSet` nouveaux sont nommés sur les fiches d’entité. Pipeline : [tuiles 48×48](../pipeline-tuiles-48.md).

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [composedtilesetcatalogentry](composedtilesetcatalogentry.md) | `composedtilesetcatalogentry → ComposedTilesetCatalogEntry` | Résumé de tileset composé, avec le nombre de tuiles |
| [composedtilesetdeleteasync](composedtilesetdeleteasync.md) | `composedtilesetdeleteasync(tilesetId, cancellationToken?) → DeleteComposedTilesetResult` | Supprime le tileset composé, ses snapshots et son historique |
| [composedtilesetentity](composedtilesetentity.md) | `composedtilesetentity → ComposedTilesetEntity` | Ligne content.composed_tilesets. Les pixels sont dans content.tiles |
| [composedtilesetlistpublishedasync](composedtilesetlistpublishedasync.md) | `composedtilesetlistpublishedasync(cancellationToken?) → IReadOnlyList<ComposedTilesetDefinition>` | Liste les tilesets dont un snapshot est publié. Un brouillon n’y est pas |
| [composedtilesetlistsummariesasync](composedtilesetlistsummariesasync.md) | `composedtilesetlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<ComposedTilesetCatalogEntry>` | Liste les résumés, avec le nombre de tuiles |
| [composedtilesetloadbyidasync](composedtilesetloadbyidasync.md) | `composedtilesetloadbyidasync(tilesetId, cancellationToken?) → StoredComposedTileset?` | Charge le brouillon courant d’un tileset composé |
| [composedtilesetmembercodec-read](composedtilesetmembercodec-read.md) | `read(json) → IReadOnlyList<ComposedTilesetMemberCodec.Row>` | Relit members_json. Une chaîne vide donne zéro ligne |
| [composedtilesetmembercodec-write](composedtilesetmembercodec-write.md) | `write(tiles) → string` | Écrit members_json : id et nom, sans les pixels |
| [composedtilesetpublicationhistoryentity](composedtilesetpublicationhistoryentity.md) | `composedtilesetpublicationhistoryentity → ComposedTilesetPublicationHistoryEntity` | Historique de publication d’un tileset composé |
| [composedtilesetpublishedsnapshotentity](composedtilesetpublishedsnapshotentity.md) | `composedtilesetpublishedsnapshotentity → ComposedTilesetPublishedSnapshotEntity` | Snapshot publié d’un tileset composé (ordre des membres, pas les pixels) |
| [composedtilesetsaveasync](composedtilesetsaveasync.md) | `composedtilesetsaveasync(request, cancellationToken?) → SaveComposedTilesetResult` | Enregistre ou publie un tileset composé. Les pixels vont dans content.tiles |
| [mapobjectcatalogentry](mapobjectcatalogentry.md) | `mapobjectcatalogentry → MapObjectCatalogEntry` | Résumé d’objet de carte pour la liste |
| [mapobjectdeleteasync](mapobjectdeleteasync.md) | `mapobjectdeleteasync(mapObjectId, cancellationToken?) → DeleteMapObjectResult` | Supprime l’objet, son historique et ses snapshots |
| [mapobjectentity](mapobjectentity.md) | `mapobjectentity → MapObjectEntity` | Ligne content.map_objects (brouillon) |
| [mapobjectlistpublishedasync](mapobjectlistpublishedasync.md) | `mapobjectlistpublishedasync(cancellationToken?) → IReadOnlyList<MapObjectDefinition>` | Liste les définitions publiées pour la palette Objets |
| [mapobjectlistsummariesasync](mapobjectlistsummariesasync.md) | `mapobjectlistsummariesasync(search?, statusFilter?, cancellationToken?) → IReadOnlyList<MapObjectCatalogEntry>` | Liste les résumés, filtrés par texte et par statut |
| [mapobjectloadbyidasync](mapobjectloadbyidasync.md) | `mapobjectloadbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?` | Charge le brouillon courant d’un objet de carte |
| [mapobjectloadpublishedbyidasync](mapobjectloadpublishedbyidasync.md) | `mapobjectloadpublishedbyidasync(mapObjectId, cancellationToken?) → StoredMapObject?` | Charge le snapshot publié d’un objet de carte |
| [mapobjectpublicationhistoryentity](mapobjectpublicationhistoryentity.md) | `mapobjectpublicationhistoryentity → MapObjectPublicationHistoryEntity` | Historique de publication d’un objet de carte |
| [mapobjectpublishedsnapshotentity](mapobjectpublishedsnapshotentity.md) | `mapobjectpublishedsnapshotentity → MapObjectPublishedSnapshotEntity` | Snapshot publié d’un objet de carte, PNG inclus |
| [mapobjectsaveasync](mapobjectsaveasync.md) | `mapobjectsaveasync(request, cancellationToken?) → SaveMapObjectResult` | Enregistre ou publie un objet de carte (révision optimiste) |
| [mobspawnzonesjson](mobspawnzonesjson.md) | `mobspawnzonesjson → string?` | Colonne JSON des zones sur le brouillon et sur le snapshot de carte |
| [savecomposedtilesetrequest](savecomposedtilesetrequest.md) | `savecomposedtilesetrequest → SaveComposedTilesetRequest` | Requête d’enregistrement d’un tileset composé |
| [savemapobjectrequest](savemapobjectrequest.md) | `savemapobjectrequest → SaveMapObjectRequest` | Requête d’enregistrement d’un objet de carte |
| [storedcomposedtileset](storedcomposedtileset.md) | `storedcomposedtileset → StoredComposedTileset` | Tileset composé relu avec sa révision |
| [storedmapobject](storedmapobject.md) | `storedmapobject → StoredMapObject` | Objet de carte relu avec sa révision |
