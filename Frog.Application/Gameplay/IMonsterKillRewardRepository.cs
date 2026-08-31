namespace Frog.Application.Gameplay;

public sealed record MonsterKillRewardRequest(
    Guid CharacterId,
    Guid MonsterInstanceId,
    long ExperienceAmount,
    int? PersistMp = null);

public sealed record MonsterKillRewardResult(
    bool Success,
    bool NewlyGranted,
    int Level,
    long Experience,
    CharacterStats? Stats,
    int MaxHp,
    int MaxMp,
    int Hp,
    int Mp,
    long ExperienceGranted)
{
    public static MonsterKillRewardResult Fail()
        => new(false, false, 0, 0, null, 0, 0, 0, 0, 0);
}

/// <summary>Octroi d'XP de kill monstre idempotent et durable (instance monstre unique).</summary>
public interface IMonsterKillRewardRepository
{
    Task<MonsterKillRewardResult> TryGrantKillRewardAsync(
        MonsterKillRewardRequest request,
        CancellationToken cancellationToken = default);
}
