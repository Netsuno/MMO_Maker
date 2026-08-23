using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresNpcRepository : INpcRepository, IPublishedNpcCatalog
{
    private readonly FrogDbContext _db;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresNpcRepository(FrogDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveNpcResult> SaveAsync(
        SaveNpcRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Definition.Validate(out var error))
        {
            return new SaveNpcResult.ValidationFailed(error ?? "NPC invalide.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveNpcResult.ValidationFailed(
                "Une opération d’enregistrement est déjà en cours.");
        }

        try
        {
            return await SaveCoreAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private async Task<SaveNpcResult> SaveCoreAsync(
        SaveNpcRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            long newRevision;
            Guid savedId;
            long? publishedRevision;

            if (request.NpcId is not Guid npcId || npcId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveNpcResult.Conflict(0);
                }

                if (request.Definition.EditorAliasId is int alias
                    && await AliasTakenAsync(alias, excludeId: null, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new SaveNpcResult.ValidationFailed("EditorAliasId déjà utilisé.");
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                _db.Npcs.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;

                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (request.Definition.EditorAliasId is int alias
                    && await AliasTakenAsync(alias, npcId, cancellationToken).ConfigureAwait(false))
                {
                    return new SaveNpcResult.ValidationFailed("EditorAliasId déjà utilisé.");
                }

                var updatedRows = await _db.Npcs
                    .Where(n => n.Id == npcId && n.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(n => n.Revision, request.ExpectedRevision + 1)
                            .SetProperty(n => n.Name, request.Definition.Name.Trim())
                            .SetProperty(n => n.Kind, request.Definition.Kind)
                            .SetProperty(
                                n => n.SpriteLogicalPath,
                                request.Definition.SpriteLogicalPath.Trim().Replace('\\', '/'))
                            .SetProperty(n => n.Level, request.Definition.Level)
                            .SetProperty(n => n.Notes, NormalizeNotes(request.Definition.Notes))
                            .SetProperty(n => n.EditorAliasId, request.Definition.EditorAliasId)
                            .SetProperty(n => n.Status, ContentPublishStatus.Draft)
                            .SetProperty(n => n.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveNpcResult.Conflict(
                        await ReadRevisionAsync(npcId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = npcId;

                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await _db.Npcs.AsNoTracking()
                        .FirstAsync(n => n.Id == npcId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(entity, now, cancellationToken)
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
            _db.ChangeTracker.Clear();
            return new SaveNpcResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            return new SaveNpcResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        NpcEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        _db.NpcPublishedSnapshots.Add(new NpcPublishedSnapshotEntity
        {
            Id = snapshotId,
            NpcId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Kind = entity.Kind,
            SpriteLogicalPath = entity.SpriteLogicalPath,
            Level = entity.Level,
            Notes = entity.Notes,
            EditorAliasId = entity.EditorAliasId,
        });
        _db.NpcPublicationHistory.Add(new NpcPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            NpcId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await _db.Npcs
            .Where(n => n.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.Status, ContentPublishStatus.Published)
                    .SetProperty(n => n.PublishedRevision, entity.Revision)
                    .SetProperty(n => n.PublishedSnapshotId, snapshotId)
                    .SetProperty(n => n.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredNpc?> LoadByIdAsync(
        Guid npcId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Npcs.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == npcId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    }

    public async Task<StoredNpc?> LoadPublishedByIdAsync(
        Guid npcId,
        CancellationToken cancellationToken = default)
    {
        var tip = await _db.Npcs.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == npcId, cancellationToken)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await _db.NpcPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        return new StoredNpc
        {
            NpcId = snapshot.NpcId,
            Definition = FromSnapshot(snapshot),
            Revision = snapshot.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = snapshot.Revision,
        };
    }

    public async Task<IReadOnlyList<NpcCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Npcs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(n =>
                EF.Functions.ILike(n.Name, $"%{value}%")
                || EF.Functions.ILike(n.SpriteLogicalPath, $"%{value}%"));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(n => n.Status == status);
        }

        return await query
            .OrderBy(n => n.Name)
            .Select(n => new NpcCatalogEntry
            {
                NpcId = n.Id,
                Name = n.Name,
                Kind = n.Kind,
                SpriteLogicalPath = n.SpriteLogicalPath,
                Level = n.Level,
                Revision = n.Revision,
                Status = n.Status,
                PublishedRevision = n.PublishedRevision,
                EditorAliasId = n.EditorAliasId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeleteNpcResult> DeleteAsync(
        Guid npcId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Npcs.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == npcId, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return new DeleteNpcResult.NotFound();
        }

        if (entity.EditorAliasId is int alias
            && await IsAliasIdReferencedByMapsAsync(alias, cancellationToken).ConfigureAwait(false))
        {
            return new DeleteNpcResult.Referenced($"NPC référencé par des cartes (alias {alias}).");
        }

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await _db.NpcPublicationHistory.Where(h => h.NpcId == npcId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.NpcPublishedSnapshots.Where(s => s.NpcId == npcId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Npcs.Where(n => n.Id == npcId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new DeleteNpcResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteNpcResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    public async Task<bool> IsAliasIdReferencedByMapsAsync(
        int editorAliasId,
        CancellationToken cancellationToken = default)
        => await _db.MapNpcSpawns.AsNoTracking()
            .AnyAsync(s => s.NpcDefinitionId == editorAliasId, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NpcDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var tips = await _db.Npcs.AsNoTracking()
            .Where(n => n.PublishedSnapshotId != null)
            .Select(n => n.PublishedSnapshotId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tips.Count == 0)
        {
            return Array.Empty<NpcDefinition>();
        }

        var snapshots = await _db.NpcPublishedSnapshots.AsNoTracking()
            .Where(s => tips.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    }

    private async Task<bool> AliasTakenAsync(int alias, Guid? excludeId, CancellationToken cancellationToken)
        => await _db.Npcs.AsNoTracking()
            .AnyAsync(
                n => n.EditorAliasId == alias && (excludeId == null || n.Id != excludeId),
                cancellationToken)
            .ConfigureAwait(false);

    private async Task<long> ReadRevisionAsync(Guid id, CancellationToken cancellationToken)
    {
        var revision = await _db.Npcs.AsNoTracking()
            .Where(n => n.Id == id)
            .Select(n => (long?)n.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static NpcEntity ToEntity(NpcDefinition definition, Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Kind = definition.Kind,
        SpriteLogicalPath = definition.SpriteLogicalPath.Trim().Replace('\\', '/'),
        Level = definition.Level,
        Notes = NormalizeNotes(definition.Notes),
        EditorAliasId = definition.EditorAliasId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredNpc ToStored(NpcEntity entity) => new()
    {
        NpcId = entity.Id,
        Definition = new NpcDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Kind = entity.Kind,
            SpriteLogicalPath = entity.SpriteLogicalPath,
            Level = entity.Level,
            Notes = entity.Notes,
            EditorAliasId = entity.EditorAliasId,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static NpcDefinition FromSnapshot(NpcPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.NpcId,
        Name = snapshot.Name,
        Kind = snapshot.Kind,
        SpriteLogicalPath = snapshot.SpriteLogicalPath,
        Level = snapshot.Level,
        Notes = snapshot.Notes,
        EditorAliasId = snapshot.EditorAliasId,
    };

    private static string? NormalizeNotes(string? notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance NPC.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
