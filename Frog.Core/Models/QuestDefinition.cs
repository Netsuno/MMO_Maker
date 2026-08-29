namespace Frog.Core.Models;

/// <summary>Quête typée publiée (Phase 8 — P8-3).</summary>
public sealed class QuestDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

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

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            error = "Description d'étape requise.";
            return false;
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
}
