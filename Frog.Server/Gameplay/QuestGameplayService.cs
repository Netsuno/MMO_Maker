using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Server.Gameplay;

/// <summary>Quêtes typées côté serveur (P8-3 / P8-R2).</summary>
public sealed class QuestGameplayService(
    IPublishedQuestCatalog quests,
    ICharacterQuestRepository progress,
    IQuestMutationRepository? questMutations = null)
{
    private readonly IPublishedQuestCatalog _quests = quests;
    private readonly ICharacterQuestRepository _progress = progress;
    private readonly IQuestMutationRepository? _questMutations = questMutations;

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
        if (existing is not null)
        {
            if (existing.Status != CharacterQuestStatus.NotStarted && !definition.Repeatable)
            {
                return $"Quête déjà démarrée: {definition.Name}";
            }

            if (existing.Status == CharacterQuestStatus.Completed && !definition.Repeatable)
            {
                return $"Quête déjà terminée: {definition.Name}";
            }
        }

        foreach (var prereqId in definition.PrerequisiteQuestIds)
        {
            var prereq = await _progress.TryGetAsync(characterId, prereqId, cancellationToken).ConfigureAwait(false);
            if (prereq?.Status != CharacterQuestStatus.Completed)
            {
                return "Prérequis de quête non satisfaits.";
            }
        }

        await _progress.UpsertAsync(
            new CharacterQuestProgress
            {
                CharacterId = characterId,
                QuestId = questId,
                Status = CharacterQuestStatus.Active,
                StageIndex = 0,
                RewardClaimed = false,
                ObjectiveCounters = new Dictionary<string, int>(StringComparer.Ordinal),
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

    public async Task<QuestTurnInResult?> TryTurnInQuestAsync(
        Guid characterId,
        Guid questId,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (_questMutations is not null)
        {
            return await _questMutations.TryTurnInAsync(characterId, questId, requestId, cancellationToken)
                .ConfigureAwait(false);
        }

        return new QuestTurnInResult(QuestTurnInStatus.Failed, "Turn-in transactionnel indisponible.");
    }

    public async Task NotifyObjectiveProgressAsync(
        Guid characterId,
        QuestObjectiveKind kind,
        QuestObjectiveSignal signal,
        CancellationToken cancellationToken = default)
    {
        var allQuests = await _quests.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        foreach (var quest in allQuests)
        {
            var prog = await _progress.TryGetAsync(characterId, quest.Id, cancellationToken).ConfigureAwait(false);
            if (prog is null || prog.Status is not (CharacterQuestStatus.Active or CharacterQuestStatus.ReadyToTurnIn))
            {
                continue;
            }

            var stageIndex = Math.Min(prog.StageIndex, quest.Stages.Count - 1);
            var stage = quest.Stages[stageIndex];
            var counters = prog.ObjectiveCounters is Dictionary<string, int> dict
                ? dict
                : new Dictionary<string, int>(prog.ObjectiveCounters, StringComparer.Ordinal);
            var changed = false;
            for (var i = 0; i < stage.Objectives.Count; i++)
            {
                var objective = stage.Objectives[i];
                if (objective.Kind != kind || !ObjectiveMatchesSignal(objective, signal))
                {
                    continue;
                }

                var key = QuestObjectiveKeys.For(stageIndex, i);
                counters.TryGetValue(key, out var current);
                if (current < objective.RequiredCount)
                {
                    counters[key] = Math.Min(objective.RequiredCount, current + signal.Increment);
                    changed = true;
                }
            }

            if (!changed)
            {
                continue;
            }

            prog.ObjectiveCounters = counters;
            if (StageObjectivesComplete(stage, stageIndex, counters))
            {
                if (stageIndex >= quest.Stages.Count - 1)
                {
                    prog.Status = CharacterQuestStatus.ReadyToTurnIn;
                }
                else
                {
                    prog.StageIndex = stageIndex + 1;
                }
            }

            prog.CharacterId = characterId;
            await _progress.UpsertAsync(prog, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<QuestJournalEntryWire>> BuildJournalAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
    {
        var progressList = await _progress.GetAllAsync(characterId, cancellationToken).ConfigureAwait(false);
        var entries = new List<QuestJournalEntryWire>();
        foreach (var prog in progressList.Where(p => p.Status != CharacterQuestStatus.NotStarted))
        {
            var definition = await _quests.TryGetPublishedByIdAsync(prog.QuestId, cancellationToken)
                .ConfigureAwait(false);
            if (definition is null)
            {
                continue;
            }

            var stageIndex = Math.Min(prog.StageIndex, definition.Stages.Count - 1);
            var stage = definition.Stages[stageIndex];
            var objectives = stage.Objectives
                .Select((o, i) =>
                {
                    var key = QuestObjectiveKeys.For(stageIndex, i);
                    prog.ObjectiveCounters.TryGetValue(key, out var current);
                    return new QuestObjectiveProgressWire
                    {
                        Description = string.IsNullOrWhiteSpace(o.Description) ? o.Kind.ToString() : o.Description,
                        Current = current,
                        Required = o.RequiredCount,
                        Completed = current >= o.RequiredCount,
                    };
                })
                .ToList();
            entries.Add(new QuestJournalEntryWire
            {
                QuestId = prog.QuestId,
                Name = definition.Name,
                Status = (byte)prog.Status,
                StageIndex = prog.StageIndex,
                StageDescription = stage.Description,
                Objectives = objectives,
            });
        }

        return entries;
    }

    private static bool ObjectiveMatchesSignal(QuestObjectiveDefinition objective, QuestObjectiveSignal signal) =>
        objective.Kind switch
        {
            QuestObjectiveKind.Talk => signal.DialogueId == objective.TargetDialogueId
                                       || signal.NpcId == objective.TargetNpcId,
            QuestObjectiveKind.Kill => signal.NpcId == objective.TargetNpcId,
            QuestObjectiveKind.Collect => signal.ItemId == objective.TargetItemId,
            QuestObjectiveKind.Visit => signal.MapId == objective.TargetMapId
                                        && (objective.TargetTileX is null || signal.TileX == objective.TargetTileX)
                                        && (objective.TargetTileY is null || signal.TileY == objective.TargetTileY),
            QuestObjectiveKind.Craft => signal.RecipeId == objective.TargetRecipeId,
            _ => false,
        };

    private static bool StageObjectivesComplete(
        QuestStageDefinition stage,
        int stageIndex,
        IReadOnlyDictionary<string, int> counters)
    {
        if (stage.Objectives.Count == 0)
        {
            return true;
        }

        for (var i = 0; i < stage.Objectives.Count; i++)
        {
            var key = QuestObjectiveKeys.For(stageIndex, i);
            counters.TryGetValue(key, out var current);
            if (current < stage.Objectives[i].RequiredCount)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed record QuestObjectiveSignal(
    int Increment = 1,
    Guid? NpcId = null,
    Guid? ItemId = null,
    Guid? RecipeId = null,
    Guid? DialogueId = null,
    int? MapId = null,
    int? TileX = null,
    int? TileY = null);
