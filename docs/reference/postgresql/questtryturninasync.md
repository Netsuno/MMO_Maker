# questtryturninasync

← [PostgreSQL](README.md) · [Référence](../README.md)

Turn-in quête transactionnel (idempotent via `requestId`).

*Source : `PostgresQuestMutationRepository.TryTurnInAsync`*

**Signature :** `questtryturninasync(characterId, questId, requestId)`

**Entrées :**
- `characterId` (`Guid`) — perso qui rend
- `questId` (`Guid`) — quête à valider
- `requestId` (`Guid`) — idempotence

**Sorties :**
- (`QuestTurnInResult`) — Status + message éventuel

**Refus :**
- paramètres invalides, requestId réutilisé avec autre quête, règles métier
