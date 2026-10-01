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

**Exemple :**
```csharp
var result = await quests.TryTurnInAsync(characterId, questId, requestId, ct);
if (result.Status is not (QuestTurnInStatus.TurnedIn or QuestTurnInStatus.IdempotentReplay))
    return; // règles métier
```
