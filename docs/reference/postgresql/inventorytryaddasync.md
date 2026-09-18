# inventorytryaddasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Ajoute une quantité d’item (stacks / slots libres).

*Source : `PostgresInventoryRepository.TryAddAsync`*

**Signature :** `inventorytryaddasync(characterId, itemId, quantity, maxStack)`

**Entrées :**
- `characterId` (`Guid`) — perso cible
- `itemId` (`Guid`) — item à ajouter
- `quantity` (`int`) — quantité demandée
- `maxStack` (`int`) — taille max de stack

**Sorties :**
- (`InventoryMutationResult`) — Status + snapshot éventuellement

**Refus :**
- quantité invalide, personnage introuvable, inventaire plein
