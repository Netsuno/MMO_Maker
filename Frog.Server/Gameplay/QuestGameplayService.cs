using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;

namespace Frog.Server.Gameplay;

/// <summary>Quêtes typées côté serveur (P8-3).</summary>
public sealed class QuestGameplayService(
    IPublishedQuestCatalog quests,
    ICharacterQuestRepository progress,
    ICharacterRepository characters,
    InventoryGameplayService inventory)
{
    private readonly IPublishedQuestCatalog _quests = quests;
    private readonly ICharacterQuestRepository _progress = progress;
    private readonly ICharacterRepository _characters = characters;
    private readonly InventoryGameplayService _inventory = inventory;

    public async Task<bool> MatchesStatusAsync(
        Guid characterId,
        Guid questId,
        string status,
        CancellationToken cancellationToken = default)
    {
        var current = await _progress.TryGetAsync(characterId, questId, cancellationToken).ConfigureAwait(false);
        var actual = current?.Status ?? CharacterQuestStatus.NotStarted;
        var expected = status switch
        {
            "not_started" => CharacterQuestStatus.NotStarted,
            "active" => CharacterQuestStatus.Active,
            "ready" => CharacterQuestStatus.ReadyToTurnIn,
            "completed" => CharacterQuestStatus.Completed,
            _ => CharacterQuestStatus.NotStarted,
        };
        return actual == expected;
    }

    public async Task<string?> TryStartQuestAsync(
        Guid characterId,
        Guid questId,
        CancellationToken cancellationToken = default)
    {
        var definition = await _quests.TryGetPublishedByIdAsync(questId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var existing = await _progress.TryGetAsync(characterId, questId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Status != CharacterQuestStatus.NotStarted)
        {
            return $"Quête déjà démarrée: {definition.Name}";
        }

        await _progress.UpsertAsync(
            new CharacterQuestProgress
            {
                CharacterId = characterId,
                QuestId = questId,
                Status = CharacterQuestStatus.Active,
                StageIndex = 0,
                RewardClaimed = false,
            },
            cancellationToken).ConfigureAwait(false);

        return $"Quête démarrée: {definition.Name}";
    }

    public async Task<string?> TryAdvanceQuestAsync(
        Guid characterId,
        Guid questId,
        int stageIndex,
        CancellationToken cancellationToken = default)
    {
        var definition = await _quests.TryGetPublishedByIdAsync(questId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var existing = await _progress.TryGetAsync(characterId, questId, cancellationToken).ConfigureAwait(false);
        if (existing is null || existing.Status is CharacterQuestStatus.NotStarted or CharacterQuestStatus.Completed)
        {
            return null;
        }

        if (stageIndex >= definition.Stages.Count)
        {
            existing.Status = CharacterQuestStatus.ReadyToTurnIn;
            existing.StageIndex = definition.Stages.Count - 1;
        }
        else
        {
            existing.StageIndex = stageIndex;
            existing.Status = CharacterQuestStatus.Active;
        }

        existing.CharacterId = characterId;
        await _progress.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        var stageDesc = definition.Stages[Math.Min(existing.StageIndex, definition.Stages.Count - 1)].Description;
        return $"Objectif: {stageDesc}";
    }

    public async Task<string?> TryTurnInQuestAsync(
        Guid characterId,
        Guid questId,
        CancellationToken cancellationToken = default)
    {
        var definition = await _quests.TryGetPublishedByIdAsync(questId, cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return null;
        }

        var existing = await _progress.TryGetAsync(characterId, questId, cancellationToken).ConfigureAwait(false);
        if (existing is null
            || existing.Status is not (CharacterQuestStatus.ReadyToTurnIn or CharacterQuestStatus.Active)
            || existing.RewardClaimed)
        {
            return null;
        }

        if (definition.CompletionReward is not null)
        {
            if (definition.CompletionReward.Gold > 0)
            {
                var character = await _characters.FindByIdAsync(characterId, cancellationToken).ConfigureAwait(false);
                if (character is null)
                {
                    return null;
                }

                var updated = character with { Gold = checked(character.Gold + definition.CompletionReward.Gold) };
                await _characters.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
            }

            if (definition.CompletionReward.ItemId is Guid itemId && definition.CompletionReward.ItemQuantity > 0)
            {
                var add = await _inventory.TryAddItemAsync(
                        characterId,
                        itemId,
                        definition.CompletionReward.ItemQuantity,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (add.Status != InventoryMutationStatus.Ok)
                {
                    return add.ErrorMessage ?? "Récompense objet impossible.";
                }
            }
        }

        existing.Status = CharacterQuestStatus.Completed;
        existing.RewardClaimed = true;
        existing.CharacterId = characterId;
        await _progress.UpsertAsync(existing, cancellationToken).ConfigureAwait(false);
        return $"Quête terminée — récompense reçue: {definition.Name}";
    }
}
