# Référence — PostgreSQL

← [Référence](../README.md)

Repositories PG, A–Z (1 fichier / fonction). Tip : `ea116afa`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [accountfindbyusernameasync](accountfindbyusernameasync.md) | `accountfindbyusernameasync(username)` | Cherche un compte |
| [accounttrycreateasync](accounttrycreateasync.md) | `accounttrycreateasync(username, password)` | Crée un compte |
| [charactercreateasync](charactercreateasync.md) | `charactercreateasync(accountId, displayName, classId, stats, maxHp, maxMp, startingSpellId, mapId, pixelX, pixelY)` | Crée un personnage |
| [characterlistbyaccountasync](characterlistbyaccountasync.md) | `characterlistbyaccountasync(accountId)` | Liste les persos |
| [charactersaveasync](charactersaveasync.md) | `charactersaveasync(character)` | Sauve un perso |
| [inventorygetasync](inventorygetasync.md) | `inventorygetasync(characterId)` | Snapshot inventaire |
| [inventorytryaddasync](inventorytryaddasync.md) | `inventorytryaddasync(…)` | Ajoute des items |
| [inventorytryremoveasync](inventorytryremoveasync.md) | `inventorytryremoveasync(characterId, slotIndex, quantity)` | Retire des items |
| [maploadbyidasync](maploadbyidasync.md) | `maploadbyidasync(mapId)` | Charge une carte stockée |
| [maploadpublishedbyidasync](maploadpublishedbyidasync.md) | `maploadpublishedbyidasync(mapId)` | Charge révision publiée |
| [mapsaveasync](mapsaveasync.md) | `mapsaveasync(request)` | Sauve / publie carte |
| [questtryturninasync](questtryturninasync.md) | `questtryturninasync(…)` | Turn-in quête transactionnel |
