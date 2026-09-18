# charactercreateasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Crée un personnage lié à un compte.

*Source : `PostgresCharacterRepository.CreateAsync`*

**Signature :** `charactercreateasync(accountId, displayName, classId, stats, maxHp, maxMp, startingSpellId, mapId, pixelX, pixelY)`

**Entrées :**
- `accountId` (`Guid`) — compte propriétaire
- `displayName` (`string`) — nom affiché
- `classId` (`Guid`) — classe de départ
- `stats` (`CharacterStats`) — stats initiales
- `maxHp` (`int`) — PV max départ
- `maxMp` (`int`) — PM max départ
- `startingSpellId` (`Guid?`) — sort initial optionnel
- `mapId` (`int`) — carte de spawn
- `pixelX` (`int`) — X pixel spawn
- `pixelY` (`int`) — Y pixel spawn

**Sorties :**
- (`CharacterCreateResult`) — Status + personnage si OK

**Refus :**
- nom invalide, classe invalide, compte introuvable, conflit de nom
