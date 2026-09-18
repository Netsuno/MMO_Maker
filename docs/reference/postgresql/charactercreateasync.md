# charactercreateasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Crée un personnage lié à un compte.

*Source : `PostgresCharacterRepository.CreateAsync`*

**Signature :** `charactercreateasync(accountId, displayName, classId, stats, maxHp, maxMp, startingSpellId, mapId, pixelX, pixelY)`

**Entrées :**
- `accountId` (`Guid`)
- `displayName` (`string`)
- `classId` (`Guid`)
- `stats` (`CharacterStats`)
- `maxHp` (`int`)
- `maxMp` (`int`)
- `startingSpellId` (`Guid?`)
- `mapId` (`int`)
- `pixelX` (`int`)
- `pixelY` (`int`)

**Sorties :**
- (`CharacterCreateResult`) — Status + personnage si OK

**Refus :**
- nom invalide, classe invalide, compte introuvable, conflit de nom
