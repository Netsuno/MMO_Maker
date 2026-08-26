namespace Frog.Application.Gameplay;

public sealed record CombatMonsterSnapshot(
    Guid InstanceId,
    Guid NpcDefinitionId,
    string Name,
    int MapId,
    int PixelX,
    int PixelY,
    int Hp,
    int MaxHp,
    int Level);

public sealed record CombatMonsterDamageAttemptResult(
    bool Success,
    string Message,
    bool MonsterKilled,
    CombatMonsterSnapshot? Monster,
    int DamageApplied)
{
    public static CombatMonsterDamageAttemptResult Fail(string message)
        => new(false, message, false, null, 0);

    public static CombatMonsterDamageAttemptResult Hit(CombatMonsterSnapshot monster, int damage)
        => new(true, "Touche.", false, monster, damage);

    public static CombatMonsterDamageAttemptResult Killed(CombatMonsterSnapshot monster, int damage)
        => new(true, "Monstre vaincu.", true, monster, damage);
}

/// <summary>Mutations combat atomiques (HP monstre verrouillé par instance).</summary>
public interface ICombatMutationRepository
{
    Task<CombatMonsterSnapshot?> SpawnMonsterAsync(
        int mapId,
        Guid npcDefinitionId,
        string name,
        int level,
        int pixelX,
        int pixelY,
        int maxHp,
        CancellationToken cancellationToken = default);

    IReadOnlyList<CombatMonsterSnapshot> ListMonstersOnMap(int mapId);

    Task<CombatMonsterDamageAttemptResult> TryApplyDamageToNamedTargetAsync(
        int mapId,
        string targetName,
        int attackerPixelX,
        int attackerPixelY,
        int rangePixels,
        int damage,
        CancellationToken cancellationToken = default);
}
