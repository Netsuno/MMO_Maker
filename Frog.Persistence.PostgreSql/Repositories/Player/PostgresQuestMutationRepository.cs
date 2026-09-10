using System.Text.Json;
using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresQuestMutationRepository(
    FrogDbContextGate gate,
    IPublishedQuestCatalog quests,
    IPublishedItemCatalog items,
    TimeProvider? clock = null) : IQuestMutationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly FrogDbContextGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IPublishedQuestCatalog _quests = quests ?? throw new ArgumentNullException(nameof(quests));
    private readonly IPublishedItemCatalog _items = items ?? throw new ArgumentNullException(nameof(items));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public Task<QuestTurnInResult> TryTurnInAsync(
        Guid characterId,
        Guid questId,
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        _gate.ExecuteAsync(async (db, ct) =>
        {
            if (characterId == Guid.Empty || questId == Guid.Empty || requestId == Guid.Empty)
            {
                return new QuestTurnInResult(QuestTurnInStatus.Failed, "Paramètres invalides.");
            }

            var existingRequest = await db.PlayerQuestTurnInRequests.AsNoTracking()
                .FirstOrDefaultAsync(r => r.CharacterId == characterId && r.RequestId == requestId, ct)
                .ConfigureAwait(false);
            if (existingRequest is not null)
            {
                if (existingRequest.QuestId != questId)
                {
                    return new QuestTurnInResult(QuestTurnInStatus.Failed, "RequestId réutilisé avec quête différente.");
                }

                var replayProgress = await db.PlayerCharacterQuestProgress.AsNoTracking()
                    .FirstOrDefaultAsync(q => q.CharacterId == characterId && q.QuestId == questId, ct)
                    .ConfigureAwait(false);
                return new QuestTurnInResult(
                    QuestTurnInStatus.IdempotentReplay,
                    "Turn-in déjà effectué.",
                    replayProgress is null ? null : MapProgress(replayProgress));
            }

            var definition = await _quests.TryGetPublishedByIdAsync(questId, ct).ConfigureAwait(false);
            if (definition is null)
            {
                return new QuestTurnInResult(QuestTurnInStatus.QuestNotFound, "Quête inconnue.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var progressRow = await db.PlayerCharacterQuestProgress
                    .FirstOrDefaultAsync(q => q.CharacterId == characterId && q.QuestId == questId, ct)
                    .ConfigureAwait(false);
                if (progressRow is null
                    || progressRow.Status is not (CharacterQuestStatus.ReadyToTurnIn or CharacterQuestStatus.Active)
                    || progressRow.RewardClaimed)
                {
                    return new QuestTurnInResult(QuestTurnInStatus.NotReady, "Quête non prête pour turn-in.");
                }

                if (!AreObjectivesComplete(definition, progressRow))
                {
                    return new QuestTurnInResult(QuestTurnInStatus.NotReady, "Objectifs incomplets.");
                }

                int? goldGranted = null;
                Guid? itemGranted = null;
                int? itemQtyGranted = null;
                if (definition.CompletionReward is not null)
                {
                    if (definition.CompletionReward.Gold > 0)
                    {
                        var character = await db.PlayerCharacters
                            .FirstOrDefaultAsync(c => c.Id == characterId, ct)
                            .ConfigureAwait(false);
                        if (character is null)
                        {
                            return new QuestTurnInResult(QuestTurnInStatus.Failed, "Personnage introuvable.");
                        }

                        character.Gold = checked(character.Gold + definition.CompletionReward.Gold);
                        goldGranted = definition.CompletionReward.Gold;
                    }

                    if (definition.CompletionReward.ItemId is Guid itemId && definition.CompletionReward.ItemQuantity > 0)
                    {
                        var itemDef = await _items.LoadPublishedByIdAsync(itemId, ct).ConfigureAwait(false);
                        if (itemDef is null)
                        {
                            return new QuestTurnInResult(QuestTurnInStatus.Failed, "Objet récompense inconnu.");
                        }

                        var invRows = await db.PlayerInventorySlots
                            .Where(s => s.CharacterId == characterId)
                            .ToListAsync(ct)
                            .ConfigureAwait(false);
                        var slots = PostgresEconomyTransactionRepository.InventorySlotsFromRows(invRows);
                        if (!PostgresEconomyTransactionRepository.TryAddToInventory(
                                slots,
                                itemId,
                                definition.CompletionReward.ItemQuantity,
                                itemDef.MaxStack))
                        {
                            return new QuestTurnInResult(QuestTurnInStatus.InventoryFull, "Inventaire plein.");
                        }

                        await PostgresEconomyTransactionRepository.PersistInventorySlotsAsync(
                                db,
                                characterId,
                                invRows,
                                slots,
                                ct)
                            .ConfigureAwait(false);
                        itemGranted = itemId;
                        itemQtyGranted = definition.CompletionReward.ItemQuantity;
                    }
                }

                progressRow.Status = CharacterQuestStatus.Completed;
                progressRow.RewardClaimed = true;
                db.PlayerQuestTurnInRequests.Add(new QuestTurnInRequestEntity
                {
                    CharacterId = characterId,
                    RequestId = requestId,
                    QuestId = questId,
                    CompletedAtUtc = _clock.GetUtcNow(),
                });

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                db.ChangeTracker.Clear();

                return new QuestTurnInResult(
                    QuestTurnInStatus.TurnedIn,
                    $"Quête terminée — récompense reçue: {definition.Name}",
                    MapProgress(progressRow),
                    goldGranted,
                    itemGranted,
                    itemQtyGranted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                return new QuestTurnInResult(QuestTurnInStatus.Failed, ex.Message);
            }
        }, cancellationToken);

    private static bool AreObjectivesComplete(QuestDefinition definition, CharacterQuestProgressEntity progress)
    {
        if (definition.Stages.Count == 0)
        {
            return false;
        }

        var counters = DeserializeCounters(progress.ObjectiveCountersJson);
        var stageIndex = Math.Min(progress.StageIndex, definition.Stages.Count - 1);
        var stage = definition.Stages[stageIndex];
        if (stage.Objectives.Count == 0)
        {
            return progress.Status == CharacterQuestStatus.ReadyToTurnIn
                   || stageIndex >= definition.Stages.Count - 1;
        }

        for (var i = 0; i < stage.Objectives.Count; i++)
        {
            var key = Frog.Core.Models.QuestObjectiveKeys.For(stageIndex, i);
            counters.TryGetValue(key, out var current);
            if (current < stage.Objectives[i].RequiredCount)
            {
                return false;
            }
        }

        return true;
    }

    internal static Dictionary<string, int> DeserializeCounters(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }

        return JsonSerializer.Deserialize<Dictionary<string, int>>(json, JsonOptions)
               ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    internal static string SerializeCounters(IReadOnlyDictionary<string, int> counters) =>
        JsonSerializer.Serialize(counters, JsonOptions);

    private static CharacterQuestProgress MapProgress(CharacterQuestProgressEntity row) =>
        new()
        {
            CharacterId = row.CharacterId,
            QuestId = row.QuestId,
            Status = row.Status,
            StageIndex = row.StageIndex,
            RewardClaimed = row.RewardClaimed,
            ObjectiveCounters = DeserializeCounters(row.ObjectiveCountersJson),
        };
}
