using System.Collections.Concurrent;
using Frog.Application.Gameplay;
using Frog.Core.Gameplay;

namespace Frog.Server.Gameplay;

public sealed class InMemoryMonsterKillRewardRepository(ICharacterRepository characters) : IMonsterKillRewardRepository
{
    private readonly ICharacterRepository _characters = characters;
    private readonly ConcurrentDictionary<(Guid CharacterId, Guid MonsterInstanceId), long> _granted = new();

    public async Task<MonsterKillRewardResult> TryGrantKillRewardAsync(
        MonsterKillRewardRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = (request.CharacterId, request.MonsterInstanceId);
        if (!_granted.TryAdd(key, request.ExperienceAmount))
        {
            var existing = await _characters.FindByIdAsync(request.CharacterId, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return MonsterKillRewardResult.Fail();
            }

            return new MonsterKillRewardResult(
                true,
                false,
                existing.Level,
                existing.Experience,
                existing.Stats,
                existing.MaxHp,
                existing.MaxMp,
                existing.Hp,
                existing.Mp,
                0);
        }

        var record = await _characters.FindByIdAsync(request.CharacterId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            _granted.TryRemove(key, out _);
            return MonsterKillRewardResult.Fail();
        }

        var (level, experience, levelsGained) = ProgressionCurve.ApplyExperience(
            record.Level,
            record.Experience,
            request.ExperienceAmount);
        var maxHp = record.MaxHp;
        var maxMp = record.MaxMp;
        var stats = record.Stats;
        var hp = record.Hp;
        var mp = request.PersistMp ?? record.Mp;
        if (levelsGained > 0)
        {
            var str = stats.Str;
            var agi = stats.Agi;
            var vit = stats.Vit;
            var intel = stats.Int;
            var dex = stats.Dex;
            var luck = stats.Luck;
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
            stats = new CharacterStats(str, agi, vit, intel, dex, luck);
            hp = maxHp;
            mp = maxMp;
        }

        try
        {
            await _characters.SaveAsync(
                record with
                {
                    Level = level,
                    Experience = experience,
                    Stats = stats,
                    MaxHp = maxHp,
                    MaxMp = maxMp,
                    Hp = hp,
                    Mp = mp,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _granted.TryRemove(key, out _);
            throw;
        }

        return new MonsterKillRewardResult(
            true,
            true,
            level,
            experience,
            stats,
            maxHp,
            maxMp,
            hp,
            mp,
            request.ExperienceAmount);
    }
}
