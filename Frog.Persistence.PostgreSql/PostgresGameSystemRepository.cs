using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresGameSystemRepository : IGameSystemRepository, IPublishedGameSystemCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresGameSystemRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveGameSystemResult> SaveAsync(
        SaveGameSystemRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveGameSystemResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var definition = GameSystemWorkspaceSession.Clone(request.Definition);
            Normalize(definition);
            if (!definition.Validate(out var error))
            {
                return new SaveGameSystemResult.ValidationFailed(error ?? "Entrée système invalide.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveGameSystemResult.ValidationFailed(
                    "Une opération d’enregistrement est déjà en cours.");
            }

            try
            {
                return await SaveCoreAsync(db, request, definition, ct).ConfigureAwait(false);
            }
            finally
            {
                _saveGate.Release();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveGameSystemResult> SaveCoreAsync(
        FrogDbContext db,
        SaveGameSystemRequest request,
        GameSystemEntryDefinition definition,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.EntryId is not Guid entryId || entryId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveGameSystemResult.Conflict(0);
                }

                var id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
                if (await DuplicateMessageAsync(db, definition, id, cancellationToken).ConfigureAwait(false) is { } duplicate)
                {
                    return new SaveGameSystemResult.ValidationFailed(duplicate);
                }

                var entity = ToEntity(definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.GameSystemEntries.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (await DuplicateMessageAsync(db, definition, entryId, cancellationToken).ConfigureAwait(false) is { } duplicate)
                {
                    return new SaveGameSystemResult.ValidationFailed(duplicate);
                }

                var updatedRows = await db.GameSystemEntries
                    .Where(s => s.Id == entryId && s.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(s => s.Revision, request.ExpectedRevision + 1)
                            .SetProperty(s => s.Kind, definition.Kind)
                            .SetProperty(s => s.Key, definition.Key)
                            .SetProperty(s => s.Label, definition.Label)
                            .SetProperty(s => s.Note, definition.Note)
                            .SetProperty(s => s.StartingBgmAsset, definition.StartingBgmAsset)
                            .SetProperty(s => s.StartingBgmVolume, definition.StartingBgmVolume)
                            .SetProperty(s => s.StartingBgmFadeMs, definition.StartingBgmFadeMs)
                            .SetProperty(s => s.Status, ContentPublishStatus.Draft)
                            .SetProperty(s => s.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveGameSystemResult.Conflict(
                        await ReadRevisionAsync(db, entryId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = entryId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.GameSystemEntries.AsNoTracking()
                        .FirstAsync(s => s.Id == entryId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, entity, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            if (TestBeforeCommitAsync is not null)
            {
                await TestBeforeCommitAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveGameSystemResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            var message = definition.Kind == GameSystemEntryKind.Options
                ? "Les options du projet existent déjà."
                : GameSystemEntryDefinition.DuplicateKeyMessage(definition.Kind, definition.Key);
            return new SaveGameSystemResult.ValidationFailed(message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveGameSystemResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        GameSystemEntryEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.GameSystemPublishedSnapshots.Add(new GameSystemPublishedSnapshotEntity
        {
            Id = snapshotId,
            EntryId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Kind = entity.Kind,
            Key = entity.Key,
            Label = entity.Label,
            Note = entity.Note,
            StartingBgmAsset = entity.StartingBgmAsset,
            StartingBgmVolume = entity.StartingBgmVolume,
            StartingBgmFadeMs = entity.StartingBgmFadeMs,
        });
        db.GameSystemPublicationHistory.Add(new GameSystemPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            EntryId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.GameSystemEntries
            .Where(s => s.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Status, ContentPublishStatus.Published)
                    .SetProperty(s => s.PublishedRevision, entity.Revision)
                    .SetProperty(s => s.PublishedSnapshotId, snapshotId)
                    .SetProperty(s => s.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredGameSystemEntry?> LoadByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredGameSystemEntry?>(async (db, ct) =>
        {
            var entity = await db.GameSystemEntries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == entryId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredGameSystemEntry?> LoadPublishedByIdAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredGameSystemEntry?>(async (db, ct) =>
        {
            var tip = await db.GameSystemEntries.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == entryId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.GameSystemPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
                .ConfigureAwait(false);
            return snapshot is null ? null : FromSnapshot(snapshot);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<GameSystemCatalogEntry>>(async (db, ct) =>
        {
            var query = db.GameSystemEntries.AsNoTracking().AsQueryable();
            if (kind is GameSystemEntryKind expected)
            {
                query = query.Where(s => s.Kind == expected);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(s =>
                    EF.Functions.ILike(s.Label, $"%{value}%")
                    || EF.Functions.ILike(s.Key, $"%{value}%")
                    || (s.Note != null && EF.Functions.ILike(s.Note, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(s => s.Status == status);
            }

            return await query
                .OrderBy(s => s.Label)
                .ThenBy(s => s.Key)
                .Select(s => new GameSystemCatalogEntry
                {
                    EntryId = s.Id,
                    Kind = s.Kind,
                    Key = s.Key,
                    Label = s.Label,
                    Revision = s.Revision,
                    Status = s.Status,
                    PublishedRevision = s.PublishedRevision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(
        GameSystemEntryKind kind,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<string>>(async (db, ct) =>
        {
            return await db.GameSystemEntries.AsNoTracking()
                .Where(s => s.Kind == kind)
                .Select(s => s.Key)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteGameSystemResult> DeleteAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteGameSystemResult>(async (db, ct) =>
        {
            if (!await db.GameSystemEntries.AsNoTracking().AnyAsync(s => s.Id == entryId, ct)
                    .ConfigureAwait(false))
            {
                return new DeleteGameSystemResult.NotFound();
            }

            await using var transaction = await db.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);
            try
            {
                await db.GameSystemPublicationHistory.Where(h => h.EntryId == entryId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.GameSystemPublishedSnapshots.Where(s => s.EntryId == entryId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.GameSystemEntries.Where(s => s.Id == entryId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteGameSystemResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteGameSystemResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameSystemEntryDefinition>> ListPublishedAsync(
        GameSystemEntryKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<GameSystemEntryDefinition>>(async (db, ct) =>
        {
            var tipsQuery = db.GameSystemEntries.AsNoTracking()
                .Where(s => s.PublishedSnapshotId != null);
            if (kind is GameSystemEntryKind expected)
            {
                tipsQuery = tipsQuery.Where(s => s.Kind == expected);
            }

            var tips = await tipsQuery
                .Select(s => s.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<GameSystemEntryDefinition>();
            }

            var snapshots = await db.GameSystemPublishedSnapshots.AsNoTracking()
                .Where(s => tips.Contains(s.Id))
                .OrderBy(s => s.Label)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(snapshot => FromSnapshot(snapshot).Definition).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> DuplicateMessageAsync(
        FrogDbContext db,
        GameSystemEntryDefinition definition,
        Guid selfId,
        CancellationToken cancellationToken)
    {
        if (definition.Kind == GameSystemEntryKind.Options)
        {
            var exists = await db.GameSystemEntries.AsNoTracking()
                .AnyAsync(
                    s => s.Id != selfId && s.Kind == GameSystemEntryKind.Options,
                    cancellationToken)
                .ConfigureAwait(false);
            return exists ? "Les options du projet existent déjà." : null;
        }

        var clash = await db.GameSystemEntries.AsNoTracking()
            .AnyAsync(
                s => s.Id != selfId && s.Kind == definition.Kind && s.Key == definition.Key,
                cancellationToken)
            .ConfigureAwait(false);
        return clash
            ? GameSystemEntryDefinition.DuplicateKeyMessage(definition.Kind, definition.Key)
            : null;
    }

    private static async Task<long> ReadRevisionAsync(
        FrogDbContext db,
        Guid id,
        CancellationToken cancellationToken)
    {
        var revision = await db.GameSystemEntries.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => (long?)s.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static GameSystemEntryEntity ToEntity(
        GameSystemEntryDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Kind = definition.Kind,
        Key = definition.Key,
        Label = definition.Label,
        Note = definition.Note,
        StartingBgmAsset = definition.StartingBgmAsset,
        StartingBgmVolume = definition.StartingBgmVolume,
        StartingBgmFadeMs = definition.StartingBgmFadeMs,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredGameSystemEntry ToStored(GameSystemEntryEntity entity) => new()
    {
        EntryId = entity.Id,
        Definition = new GameSystemEntryDefinition
        {
            Id = entity.Id,
            Kind = entity.Kind,
            Key = entity.Key,
            Label = entity.Label,
            Note = entity.Note,
            StartingBgmAsset = entity.StartingBgmAsset,
            StartingBgmVolume = entity.StartingBgmVolume,
            StartingBgmFadeMs = entity.StartingBgmFadeMs,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static StoredGameSystemEntry FromSnapshot(GameSystemPublishedSnapshotEntity snapshot) => new()
    {
        EntryId = snapshot.EntryId,
        Definition = new GameSystemEntryDefinition
        {
            Id = snapshot.EntryId,
            Kind = snapshot.Kind,
            Key = snapshot.Key,
            Label = snapshot.Label,
            Note = snapshot.Note,
            StartingBgmAsset = snapshot.StartingBgmAsset,
            StartingBgmVolume = snapshot.StartingBgmVolume,
            StartingBgmFadeMs = snapshot.StartingBgmFadeMs,
        },
        Revision = snapshot.Revision,
        Status = ContentPublishStatus.Published,
        PublishedRevision = snapshot.Revision,
    };

    private static void Normalize(GameSystemEntryDefinition definition)
    {
        definition.Label = definition.Label.Trim();
        definition.Note = string.IsNullOrWhiteSpace(definition.Note) ? null : definition.Note.Trim();
        if (definition.Kind == GameSystemEntryKind.Options)
        {
            definition.Key = string.Empty;
            if (MapAudioTrack.TryCreate(
                    definition.StartingBgmAsset,
                    definition.StartingBgmVolume,
                    definition.StartingBgmFadeMs,
                    "Musique de départ",
                    out var track,
                    out _))
            {
                definition.StartingBgmAsset = track.Asset;
                definition.StartingBgmVolume = track.Volume;
                definition.StartingBgmFadeMs = track.FadeMs;
            }

            return;
        }

        definition.Key = definition.Key.Trim();
        definition.StartingBgmAsset = string.Empty;
        definition.StartingBgmVolume = MapAudioTrack.DefaultVolume;
        definition.StartingBgmFadeMs = 0;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return true;
            }
        }

        return false;
    }

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance système.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
