using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresMonsterKillRewardRepository : IMonsterKillRewardRepository
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    /// <summary>Seam de test : leve une exception apres mutations, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresMonsterKillRewardRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public Task<MonsterKillRewardResult> TryGrantKillRewardAsync(
        Guid characterId,
        Guid monsterInstanceId,
        long experienceAmount,
        int currentLevel,
        long currentExperience,
        CharacterStats currentStats,
        int currentMaxHp,
        int currentMaxMp,
        int currentHp,
        int currentMp,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var existing = await db.PlayerMonsterKillRewards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.CharacterId == characterId && r.MonsterInstanceId == monsterInstanceId,
                        ct)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    var character = await db.PlayerCharacters
                        .AsNoTracking()
                        .SingleAsync(c => c.Id == characterId, ct)
                        .ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return ToResult(character, newlyGranted: false, 0);
                }

                if (!await TryLockCharacterAsync(db, characterId, ct).ConfigureAwait(false))
                {
                    return MonsterKillRewardResult.Fail();
                }

                var row = await db.PlayerCharacters
                    .SingleAsync(c => c.Id == characterId, ct)
                    .ConfigureAwait(false);

                var (level, experience, levelsGained) = ProgressionCurve.ApplyExperience(
                    currentLevel,
                    currentExperience,
                    experienceAmount);
                var maxHp = currentMaxHp;
                var maxMp = currentMaxMp;
                var str = currentStats.Str;
                var agi = currentStats.Agi;
                var vit = currentStats.Vit;
                var intel = currentStats.Int;
                var dex = currentStats.Dex;
                var luck = currentStats.Luck;
                var hp = currentHp;
                var mp = currentMp;
                if (levelsGained > 0)
                {
                    ProgressionCurve.ApplyLevelUpBonuses(
                        ref maxHp,
                        ref maxMp,
                        ref str,
                        ref agi,
                        ref vit,
                        ref intel,
                        ref dex,
                        ref luck,
                        levelsGained);
                    hp = maxHp;
                    mp = maxMp;
                }

                row.Level = level;
                row.Experience = experience;
                row.MaxHp = maxHp;
                row.MaxMp = maxMp;
                row.Hp = hp;
                row.Mp = mp;
                row.Str = str;
                row.Agi = agi;
                row.Vit = vit;
                row.Int = intel;
                row.Dex = dex;
                row.Luck = luck;
                row.UpdatedAtUtc = _clock.GetUtcNow();

                db.PlayerMonsterKillRewards.Add(new MonsterKillRewardEntity
                {
                    CharacterId = characterId,
                    MonsterInstanceId = monsterInstanceId,
                    ExperienceAmount = experienceAmount,
                    GrantedAtUtc = _clock.GetUtcNow(),
                });

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return ToResult(row, newlyGranted: true, experienceAmount);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }, cancellationToken);

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
        => await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);

    private static MonsterKillRewardResult ToResult(CharacterEntity row, bool newlyGranted, long xpGranted)
        => new(
            true,
            newlyGranted,
            row.Level,
            row.Experience,
            new CharacterStats(row.Str, row.Agi, row.Vit, row.Int, row.Dex, row.Luck),
            row.MaxHp,
            row.MaxMp,
            row.Hp,
            row.Mp,
            xpGranted);
}
