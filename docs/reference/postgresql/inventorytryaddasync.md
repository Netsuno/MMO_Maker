# inventorytryaddasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Ajoute une quantité d’item (stacks / slots libres).

*Source : `PostgresInventoryRepository.TryAddAsync`*

**Signature :** `inventorytryaddasync(characterId, itemId, quantity, maxStack)`

**Entrées :**
- `characterId` (`Guid`)
- `itemId` (`Guid`)
- `quantity` (`int`)
- `maxStack` (`int`)

**Sorties :**
- (`InventoryMutationResult`) — Status + snapshot éventuellement

**Refus :**
- quantité invalide, personnage introuvable, inventaire plein
