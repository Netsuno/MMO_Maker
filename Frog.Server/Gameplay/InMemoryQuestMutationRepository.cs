using System.Collections.Concurrent;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;
using Frog.Server.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryQuestMutationRepository(
    ICharacterQuestRepository progress,
    ICharacterRepository characters,
    InventoryGameplayService inventory,
    IPublishedQuestCatalog quests) : IQuestMutationRepository
{
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid RequestId), QuestTurnInResult> _completed = new();

    public async Task<QuestTurnInResult> TryTurnInAsync(
        Guid characterId,
        Guid questId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (_completed.TryGetValue((characterId, requestId), out var replay))
        {
            return replay with { Status = QuestTurnInStatus.IdempotentReplay };
        }

        var definition = await quests.TryGetPublishedByIdAsync(questId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return new QuestTurnInResult(QuestTurnInStatus.QuestNotFound, "Quête inconnue.");
        }

        var existing = await progress.TryGetAsync(characterId, questId, cancellationToken).ConfigureAwait(false);
        if (existing is null
            || existing.Status is not (CharacterQuestStatus.ReadyToTurnIn or CharacterQuestStatus.Active)
            || existing.RewardClaimed)
        {
            return new QuestTurnInResult(QuestTurnInStatus.NotReady, "Quête non prête.");
        }

        if (definition.CompletionReward?.Gold > 0)
        {
            var character = await characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
            if (character is null)
            {
                return new QuestTurnInResult(QuestTurnInStatus.Failed, "Personnage introuvable.");
            }

            await characters.SaveAsync(
                character with { Gold = checked(character.Gold + definition.CompletionReward.Gold) },
                cancellationToken).ConfigureAwait(false);
        }

        if (definition.CompletionReward?.ItemId is Guid itemId && definition.CompletionReward.ItemQuantity > 0)
        {
            var add = await inventory.TryAddItemAsync(
                    characterId,
                    itemId,
                    definition.CompletionReward.ItemQuantity,
                    cancellationToken)
                .ConfigureAwait(false);
            if (add.Status != InventoryMutationStatus.Ok)
            {
                return new QuestTurnInResult(QuestTurnInStatus.InventoryFull, add.ErrorMessage);
            }
        }

        existing.Status = CharacterQuestStatus.Completed;
        existing.RewardClaimed = true;
        await progress.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        var result = new QuestTurnInResult(
            QuestTurnInStatus.TurnedIn,
            $"Quête terminée — récompense reçue: {definition.Name}",
            existing);
        _completed[(characterId, requestId)] = result;
        return result;
    }
}
