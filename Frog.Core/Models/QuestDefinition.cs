namespace Frog.Core.Models;

/// <summary>Quête typée publiée (Phase 8 — P8-3).</summary>
public sealed class QuestDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

    /// <summary>Quêtes prérequises (doivent être complétées avant démarrage).</summary>
    public IReadOnlyList<Guid> PrerequisiteQuestIds { get; set; } = Array.Empty<Guid>();

    /// <summary>Peut être redémarrée après complétion.</summary>
    public bool Repeatable { get; set; }

    public IReadOnlyList<QuestStageDefinition> Stages { get; set; } = Array.Empty<QuestStageDefinition>();

    public QuestRewardDefinition? CompletionReward { get; set; }

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de quête requis.";
            return false;
        }

        if (Stages.Count == 0)
        {
            error = "Au moins une étape requise.";
            return false;
        }

        for (var i = 0; i < Stages.Count; i++)
        {
            if (!Stages[i].Validate(out error))
            {
                error = $"Étape {i}: {error}";
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class QuestStageDefinition
{
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<QuestObjectiveDefinition> Objectives { get; set; } = Array.Empty<QuestObjectiveDefinition>();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            error = "Description d'étape requise.";
            return false;
        }

        for (var i = 0; i < Objectives.Count; i++)
        {
            if (!Objectives[i].Validate(out error))
            {
                error = $"Objectif {i}: {error}";
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class QuestObjectiveDefinition
{
    public QuestObjectiveKind Kind { get; set; }

    public string Description { get; set; } = string.Empty;

    public int RequiredCount { get; set; } = 1;

    public Guid? TargetNpcId { get; set; }

    public Guid? TargetItemId { get; set; }

    public Guid? TargetRecipeId { get; set; }

    public int? TargetMapId { get; set; }

    public int? TargetTileX { get; set; }

    public int? TargetTileY { get; set; }

    public Guid? TargetDialogueId { get; set; }

    public bool Validate(out string? error)
    {
        if (RequiredCount < 1)
        {
            error = "RequiredCount doit être >= 1.";
            return false;
        }

        switch (Kind)
        {
            case QuestObjectiveKind.Talk:
                if (TargetDialogueId is null && TargetNpcId is null)
                {
                    error = "talk requiert TargetDialogueId ou TargetNpcId.";
                    return false;
                }

                break;
            case QuestObjectiveKind.Kill:
                if (TargetNpcId is null)
                {
                    error = "kill requiert TargetNpcId.";
                    return false;
                }

                break;
            case QuestObjectiveKind.Collect:
                if (TargetItemId is null)
                {
                    error = "collect requiert TargetItemId.";
                    return false;
                }

                break;
            case QuestObjectiveKind.Visit:
                if (TargetMapId is null)
                {
                    error = "visit requiert TargetMapId.";
                    return false;
                }

                break;
            case QuestObjectiveKind.Craft:
                if (TargetRecipeId is null)
                {
                    error = "craft requiert TargetRecipeId.";
                    return false;
                }

                break;
        }

        error = null;
        return true;
    }
}

public sealed class QuestRewardDefinition
{
    public int Gold { get; set; }

    public Guid? ItemId { get; set; }

    public int ItemQuantity { get; set; } = 1;
}

public enum CharacterQuestStatus
{
    NotStarted = 0,
    Active = 1,
    ReadyToTurnIn = 2,
    Completed = 3,
}

public sealed class CharacterQuestProgress
{
    public Guid CharacterId { get; set; }

    public Guid QuestId { get; set; }

    public CharacterQuestStatus Status { get; set; }

    public int StageIndex { get; set; }

    public bool RewardClaimed { get; set; }

    /// <summary>Compteurs par clé objectif (stageIndex:objectiveIndex).</summary>
    public IReadOnlyDictionary<string, int> ObjectiveCounters { get; set; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
}
