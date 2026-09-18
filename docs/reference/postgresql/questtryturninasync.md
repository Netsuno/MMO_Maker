# questtryturninasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Turn-in quête transactionnel (idempotent via `requestId`).

*Source : `PostgresQuestMutationRepository.TryTurnInAsync`*

**Signature :** `questtryturninasync(characterId, questId, requestId)`

**Entrées :**
- `characterId` (`Guid`)
- `questId` (`Guid`)
- `requestId` (`Guid`) — idempotence

**Sorties :**
- (`QuestTurnInResult`) — Status + message éventuel

**Refus :**
- paramètres invalides, requestId réutilisé avec autre quête, règles métier
