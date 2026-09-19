# inventorytryremoveasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Retire une quantité depuis un slot inventaire.

*Source : `PostgresInventoryRepository.TryRemoveAsync`*

**Signature :** `inventorytryremoveasync(characterId, slotIndex, quantity)`

**Entrées :**
- `characterId` (`Guid`) — perso cible
- `slotIndex` (`int`) — index de slot
- `quantity` (`int`) — quantité à retirer

**Sorties :**
- (`InventoryMutationResult`) — Status + snapshot éventuellement

**Refus :**
- slot invalide, quantité insuffisante, personnage introuvable
