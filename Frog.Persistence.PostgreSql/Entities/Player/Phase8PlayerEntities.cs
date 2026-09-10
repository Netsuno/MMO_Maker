using Frog.Core.Models;

namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class CharacterQuestProgressEntity
{
    public Guid CharacterId { get; set; }

    public Guid QuestId { get; set; }

    public CharacterQuestStatus Status { get; set; }

    public int StageIndex { get; set; }

    public bool RewardClaimed { get; set; }

    public string ObjectiveCountersJson { get; set; } = "{}";
}

public sealed class QuestTurnInRequestEntity
{
    public Guid CharacterId { get; set; }

    public Guid RequestId { get; set; }

    public Guid QuestId { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }
}

public sealed class CharacterProfessionProgressEntity
{
    public Guid CharacterId { get; set; }

    public Guid ProfessionId { get; set; }

    public int Level { get; set; }

    public long Experience { get; set; }
}

public sealed class EventCraftRequestEntity
{
    public Guid CharacterId { get; set; }

    public Guid RequestId { get; set; }

    public Guid RecipeId { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }
}

public sealed class MapEventExecutionRequestEntity
{
    public Guid CharacterId { get; set; }

    public Guid RequestId { get; set; }

    public long PlacementId { get; set; }

    public int CatalogAliasId { get; set; }

    public string ResultJson { get; set; } = "{}";

    public DateTimeOffset CompletedAtUtc { get; set; }
}
