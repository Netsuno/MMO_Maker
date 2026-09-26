using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresSystemFlagRepository : ISystemFlagRepository, IPublishedSystemFlagCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresSystemFlagRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveSystemFlagResult> SaveAsync(
        SaveSystemFlagRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveSystemFlagResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Definition.Validate(out var error))
            {
                return new SaveSystemFlagResult.ValidationFailed(error ?? "Entrée système invalide.");
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveSystemFlagResult.ValidationFailed(
                    "Une opération d’enregistrement est déjà en cours.");
            }

            try
            {
                return await SaveCoreAsync(db, request, ct).ConfigureAwait(false);
            }
            finally
            {
                _saveGate.Release();
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveSystemFlagResult> SaveCoreAsync(
        FrogDbContext db,
        SaveSystemFlagRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var definition = SystemFlagWorkspaceSession.Clone(request.Definition);
        definition.Key = definition.Key.Trim();
        definition.Label = definition.Label.Trim();
        definition.Note = string.IsNullOrWhiteSpace(definition.Note) ? null : definition.Note.Trim();

        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.FlagId is not Guid flagId || flagId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveSystemFlagResult.Conflict(0);
                }

                var id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
                if (await KeyTakenAsync(db, definition.Kind, definition.Key, exceptId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new SaveSystemFlagResult.ValidationFailed(DuplicateKeyMessage(definition.Kind));
                }

                var entity = ToEntity(definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.SystemFlags.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (await KeyTakenAsync(db, definition.Kind, definition.Key, flagId, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new SaveSystemFlagResult.ValidationFailed(DuplicateKeyMessage(definition.Kind));
                }

                var updatedRows = await db.SystemFlags
                    .Where(flag => flag.Id == flagId && flag.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(flag => flag.Revision, request.ExpectedRevision + 1)
                            .SetProperty(flag => flag.Kind, definition.Kind)
                            .SetProperty(flag => flag.Key, definition.Key)
                            .SetProperty(flag => flag.Label, definition.Label)
                            .SetProperty(flag => flag.Note, definition.Note)
                            .SetProperty(flag => flag.Status, ContentPublishStatus.Draft)
                            .SetProperty(flag => flag.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveSystemFlagResult.Conflict(
                        await ReadRevisionAsync(db, flagId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = flagId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.SystemFlags.AsNoTracking()
                        .FirstAsync(flag => flag.Id == flagId, cancellationToken)
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
            return new SaveSystemFlagResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveSystemFlagResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        SystemFlagEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.SystemFlagPublishedSnapshots.Add(new SystemFlagPublishedSnapshotEntity
        {
            Id = snapshotId,
            FlagId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Kind = entity.Kind,
            Key = entity.Key,
            Label = entity.Label,
            Note = entity.Note,
        });
        db.SystemFlagPublicationHistory.Add(new SystemFlagPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            FlagId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.SystemFlags
            .Where(flag => flag.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(flag => flag.Status, ContentPublishStatus.Published)
                    .SetProperty(flag => flag.PublishedRevision, entity.Revision)
                    .SetProperty(flag => flag.PublishedSnapshotId, snapshotId)
                    .SetProperty(flag => flag.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredSystemFlag?> LoadByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredSystemFlag?>(async (db, ct) =>
        {
            var entity = await db.SystemFlags.AsNoTracking()
                .FirstOrDefaultAsync(flag => flag.Id == flagId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredSystemFlag?> LoadPublishedByIdAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredSystemFlag?>(async (db, ct) =>
        {
            var tip = await db.SystemFlags.AsNoTracking()
                .FirstOrDefaultAsync(flag => flag.Id == flagId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.SystemFlagPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(flag => flag.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredSystemFlag
            {
                FlagId = snapshot.FlagId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SystemFlagCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        SystemFlagKind? kindFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<SystemFlagCatalogEntry>>(async (db, ct) =>
        {
            var query = db.SystemFlags.AsNoTracking().AsQueryable();
            if (kindFilter is { } kind)
            {
                query = query.Where(flag => flag.Kind == kind);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(flag =>
                    EF.Functions.ILike(flag.Label, $"%{value}%")
                    || EF.Functions.ILike(flag.Key, $"%{value}%")
                    || (flag.Note != null && EF.Functions.ILike(flag.Note, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(flag => flag.Status == status);
            }

            return await query
                .OrderBy(flag => flag.Label)
                .ThenBy(flag => flag.Key)
                .Select(flag => new SystemFlagCatalogEntry
                {
                    FlagId = flag.Id,
                    Kind = flag.Kind,
                    Key = flag.Key,
                    Label = flag.Label,
                    Revision = flag.Revision,
                    Status = flag.Status,
                    PublishedRevision = flag.PublishedRevision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteSystemFlagResult> DeleteAsync(
        Guid flagId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteSystemFlagResult>(async (db, ct) =>
        {
            if (!await db.SystemFlags.AsNoTracking().AnyAsync(flag => flag.Id == flagId, ct).ConfigureAwait(false))
            {
                return new DeleteSystemFlagResult.NotFound();
            }

            await using var transaction = await db.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);
            try
            {
                await db.SystemFlagPublicationHistory.Where(h => h.FlagId == flagId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.SystemFlagPublishedSnapshots.Where(s => s.FlagId == flagId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.SystemFlags.Where(flag => flag.Id == flagId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteSystemFlagResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteSystemFlagResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SystemFlagDefinition>> ListPublishedAsync(
        SystemFlagKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<SystemFlagDefinition>>(async (db, ct) =>
        {
            var tips = db.SystemFlags.AsNoTracking().Where(flag => flag.PublishedSnapshotId != null);
            if (kind is { } filter)
            {
                tips = tips.Where(flag => flag.Kind == filter);
            }

            var snapshotIds = await tips
                .Select(flag => flag.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (snapshotIds.Count == 0)
            {
                return Array.Empty<SystemFlagDefinition>();
            }

            var snapshots = await db.SystemFlagPublishedSnapshots.AsNoTracking()
                .Where(flag => snapshotIds.Contains(flag.Id))
                .OrderBy(flag => flag.Label)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(FromSnapshot).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> KeyTakenAsync(
        FrogDbContext db,
        SystemFlagKind kind,
        string key,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var drafts = db.SystemFlags.AsNoTracking()
            .Where(flag => flag.Kind == kind && flag.Key == key);
        if (exceptId is Guid draftId)
        {
            drafts = drafts.Where(flag => flag.Id != draftId);
        }

        if (await drafts.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var currentTips = db.SystemFlags.AsNoTracking()
            .Where(flag => flag.PublishedSnapshotId != null);
        if (exceptId is Guid publishedId)
        {
            currentTips = currentTips.Where(flag => flag.Id != publishedId);
        }

        var snapshotIds = currentTips.Select(flag => flag.PublishedSnapshotId!.Value);
        return await db.SystemFlagPublishedSnapshots.AsNoTracking()
            .AnyAsync(
                snapshot => snapshotIds.Contains(snapshot.Id)
                    && snapshot.Kind == kind
                    && snapshot.Key == key,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.SystemFlags.AsNoTracking()
            .Where(flag => flag.Id == id)
            .Select(flag => (long?)flag.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static SystemFlagEntity ToEntity(SystemFlagDefinition definition, Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Kind = definition.Kind,
        Key = definition.Key,
        Label = definition.Label,
        Note = definition.Note,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredSystemFlag ToStored(SystemFlagEntity entity) => new()
    {
        FlagId = entity.Id,
        Definition = new SystemFlagDefinition
        {
            Id = entity.Id,
            Kind = entity.Kind,
            Key = entity.Key,
            Label = entity.Label,
            Note = entity.Note,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static SystemFlagDefinition FromSnapshot(SystemFlagPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.FlagId,
        Kind = snapshot.Kind,
        Key = snapshot.Key,
        Label = snapshot.Label,
        Note = snapshot.Note,
    };

    private static string DuplicateKeyMessage(SystemFlagKind kind)
        => kind == SystemFlagKind.Variable
            ? "Cet identifiant de variable existe déjà."
            : "Cet identifiant d’interrupteur existe déjà.";

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance du catalogue système.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
