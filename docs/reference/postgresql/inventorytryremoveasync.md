# inventorytryremoveasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Retire une quantité depuis un slot inventaire.

*Source : `PostgresInventoryRepository.TryRemoveAsync`*

**Signature :** `inventorytryremoveasync(characterId, slotIndex, quantity)`

**Entrées :**
- `characterId` (`Guid`)
- `slotIndex` (`int`) — index de slot
- `quantity` (`int`)

**Sorties :**
- (`InventoryMutationResult`)

**Refus :**
- slot invalide, quantité insuffisante, personnage introuvable
