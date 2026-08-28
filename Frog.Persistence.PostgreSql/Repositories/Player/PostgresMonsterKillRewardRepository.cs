using Frog.Application.Gameplay;
using Frog.Core.Gameplay;
using Frog.Persistence.PostgreSql.Entities.Player;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        MonsterKillRewardRequest request,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                var existing = await db.PlayerMonsterKillRewards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        r => r.CharacterId == request.CharacterId && r.MonsterInstanceId == request.MonsterInstanceId,
                        ct)
                    .ConfigureAwait(false);
                if (existing is not null)
                {
                    var replayCharacter = await db.PlayerCharacters
                        .AsNoTracking()
                        .SingleAsync(c => c.Id == request.CharacterId, ct)
                        .ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return ToResult(replayCharacter, newlyGranted: false, 0);
                }

                if (!await TryLockCharacterAsync(db, request.CharacterId, ct).ConfigureAwait(false))
                {
                    return MonsterKillRewardResult.Fail();
                }

                var row = await db.PlayerCharacters
                    .SingleAsync(c => c.Id == request.CharacterId, ct)
                    .ConfigureAwait(false);

                var (level, experience, levelsGained) = ProgressionCurve.ApplyExperience(
                    row.Level,
                    row.Experience,
                    request.ExperienceAmount);
                var maxHp = row.MaxHp;
                var maxMp = row.MaxMp;
                var str = row.Str;
                var agi = row.Agi;
                var vit = row.Vit;
                var intel = row.Int;
                var dex = row.Dex;
                var luck = row.Luck;
                var hp = row.Hp;
                var mp = request.PersistMp ?? row.Mp;
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
                    CharacterId = request.CharacterId,
                    MonsterInstanceId = request.MonsterInstanceId,
                    ExperienceAmount = request.ExperienceAmount,
                    GrantedAtUtc = _clock.GetUtcNow(),
                });

                if (TestBeforeCommitAsync is not null)
                {
                    await TestBeforeCommitAsync(ct).ConfigureAwait(false);
                }

                try
                {
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                }
                catch (DbUpdateException ex) when (IsLedgerDuplicate(ex))
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    db.ChangeTracker.Clear();
                    return await LoadReplayResultAsync(db, request, ct).ConfigureAwait(false);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return ToResult(row, newlyGranted: true, request.ExperienceAmount);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                db.ChangeTracker.Clear();
                throw;
            }
        }, cancellationToken);

    private static async Task<MonsterKillRewardResult> LoadReplayResultAsync(
        FrogDbContext db,
        MonsterKillRewardRequest request,
        CancellationToken ct)
    {
        var replayCharacter = await db.PlayerCharacters
            .AsNoTracking()
            .SingleAsync(c => c.Id == request.CharacterId, ct)
            .ConfigureAwait(false);
        return ToResult(replayCharacter, newlyGranted: false, 0);
    }

    private static bool IsLedgerDuplicate(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static async Task<bool> TryLockCharacterAsync(FrogDbContext db, Guid characterId, CancellationToken ct)
    {
        var exists = await db.PlayerCharacters.AnyAsync(c => c.Id == characterId, ct).ConfigureAwait(false);
        if (!exists)
        {
            return false;
        }

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM player.characters WHERE id = {characterId} FOR UPDATE",
            ct).ConfigureAwait(false);
        return true;
    }

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
