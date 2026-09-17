using Frog.Application.Gameplay;
using Frog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql.Repositories.Player;

public sealed class PostgresCharacterQuestRepository(FrogDbContextGate gate) : ICharacterQuestRepository
{
    public Task<IReadOnlyList<CharacterQuestProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => gate.ExecuteAsync<IReadOnlyList<CharacterQuestProgress>>(async (db, ct) =>
        {
            var rows = await db.PlayerCharacterQuestProgress.AsNoTracking()
                .Where(q => q.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows.Select(Map).ToList();
        }, cancellationToken);

    public Task<CharacterQuestProgress?> TryGetAsync(
        Guid characterId,
        Guid questId,
        CancellationToken cancellationToken = default)
        => gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.PlayerCharacterQuestProgress.AsNoTracking()
                .FirstOrDefaultAsync(q => q.CharacterId == characterId && q.QuestId == questId, ct)
                .ConfigureAwait(false);
            return row is null ? null : Map(row);
        }, cancellationToken);

    public Task UpsertAsync(CharacterQuestProgress progress, CancellationToken cancellationToken = default)
        => gate.ExecuteAsync(async (db, ct) =>
        {
            var existing = await db.PlayerCharacterQuestProgress
                .FirstOrDefaultAsync(q => q.CharacterId == progress.CharacterId && q.QuestId == progress.QuestId, ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.PlayerCharacterQuestProgress.Add(new Entities.Player.CharacterQuestProgressEntity
                {
                    CharacterId = progress.CharacterId,
                    QuestId = progress.QuestId,
                    Status = progress.Status,
                    StageIndex = progress.StageIndex,
                    RewardClaimed = progress.RewardClaimed,
                    ObjectiveCountersJson = PostgresQuestMutationRepository.SerializeCounters(progress.ObjectiveCounters),
                });
            }
            else
            {
                existing.Status = progress.Status;
                existing.StageIndex = progress.StageIndex;
                existing.RewardClaimed = progress.RewardClaimed;
                existing.ObjectiveCountersJson =
                    PostgresQuestMutationRepository.SerializeCounters(progress.ObjectiveCounters);
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken);

    private static CharacterQuestProgress Map(Entities.Player.CharacterQuestProgressEntity row) =>
        new()
        {
            CharacterId = row.CharacterId,
            QuestId = row.QuestId,
            Status = row.Status,
            StageIndex = row.StageIndex,
            RewardClaimed = row.RewardClaimed,
            ObjectiveCounters = PostgresQuestMutationRepository.DeserializeCounters(row.ObjectiveCountersJson),
        };
}

public sealed class PostgresCharacterProfessionRepository(FrogDbContextGate gate) : ICharacterProfessionRepository
{
    public Task<IReadOnlyList<CharacterProfessionProgress>> GetAllAsync(
        Guid characterId,
        CancellationToken cancellationToken = default)
        => gate.ExecuteAsync<IReadOnlyList<CharacterProfessionProgress>>(async (db, ct) =>
        {
            var rows = await db.PlayerCharacterProfessionProgress.AsNoTracking()
                .Where(p => p.CharacterId == characterId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows.Select(Map).ToList();
        }, cancellationToken);

    public Task<CharacterProfessionProgress?> TryGetAsync(
        Guid characterId,
        Guid professionId,
        CancellationToken cancellationToken = default)
        => gate.ExecuteAsync(async (db, ct) =>
        {
            var row = await db.PlayerCharacterProfessionProgress.AsNoTracking()
                .FirstOrDefaultAsync(p => p.CharacterId == characterId && p.ProfessionId == professionId, ct)
                .ConfigureAwait(false);
            return row is null ? null : Map(row);
        }, cancellationToken);

    public Task UpsertAsync(CharacterProfessionProgress progress, CancellationToken cancellationToken = default)
        => gate.ExecuteAsync(async (db, ct) =>
        {
            var existing = await db.PlayerCharacterProfessionProgress
                .FirstOrDefaultAsync(
                    p => p.CharacterId == progress.CharacterId && p.ProfessionId == progress.ProfessionId,
                    ct)
                .ConfigureAwait(false);
            if (existing is null)
            {
                db.PlayerCharacterProfessionProgress.Add(new Entities.Player.CharacterProfessionProgressEntity
                {
                    CharacterId = progress.CharacterId,
                    ProfessionId = progress.ProfessionId,
                    Level = progress.Level,
                    Experience = progress.Experience,
                });
            }
            else
            {
                existing.Level = progress.Level;
                existing.Experience = progress.Experience;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();
        }, cancellationToken);

    private static CharacterProfessionProgress Map(Entities.Player.CharacterProfessionProgressEntity row) =>
        new()
        {
            CharacterId = row.CharacterId,
            ProfessionId = row.ProfessionId,
            Level = row.Level,
            Experience = row.Experience,
        };
}
