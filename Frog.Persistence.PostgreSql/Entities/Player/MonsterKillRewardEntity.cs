namespace Frog.Persistence.PostgreSql.Entities.Player;

public sealed class MonsterKillRewardEntity
{
    public Guid CharacterId { get; set; }

    public Guid MonsterInstanceId { get; set; }

    public long ExperienceAmount { get; set; }

    public DateTimeOffset GrantedAtUtc { get; set; }
}
