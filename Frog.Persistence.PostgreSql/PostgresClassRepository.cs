using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresClassRepository : IClassRepository, IPublishedClassCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly ISpellRepository _spells;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresClassRepository(
        FrogDbContextGate gate,
        ISpellRepository? spells = null,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _spells = spells ?? new PostgresSpellRepository(gate);
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveClassResult> SaveAsync(
        SaveClassRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveClassResult>(async (db, ct) =>
        {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return new SaveClassResult.ValidationFailed(error ?? "Classe invalide.");
        }

        if (request.Definition.StartingSpellId is Guid startingSpellId
            && await _spells.LoadPublishedByIdAsync(startingSpellId, ct)
                .ConfigureAwait(false) is null)
        {
            return new SaveClassResult.ValidationFailed(
                "Le sort de départ doit exister dans le catalogue publié.");
        }

        if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            return new SaveClassResult.ValidationFailed(
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

    private async Task<SaveClassResult> SaveCoreAsync(FrogDbContext db, SaveClassRequest request,
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

            if (request.ClassId is not Guid classId || classId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveClassResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.Classes.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.Classes
                    .Where(c => c.Id == classId && c.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(c => c.Revision, request.ExpectedRevision + 1)
                            .SetProperty(c => c.Name, request.Definition.Name.Trim())
                            .SetProperty(c => c.Description, NormalizeDescription(request.Definition.Description))
                            .SetProperty(c => c.BaseHp, request.Definition.BaseHp)
                            .SetProperty(c => c.BaseMp, request.Definition.BaseMp)
                            .SetProperty(c => c.Str, request.Definition.Str)
                            .SetProperty(c => c.Agi, request.Definition.Agi)
                            .SetProperty(c => c.Vit, request.Definition.Vit)
                            .SetProperty(c => c.Int, request.Definition.Int)
                            .SetProperty(c => c.Dex, request.Definition.Dex)
                            .SetProperty(c => c.Luck, request.Definition.Luck)
                            .SetProperty(c => c.StartingSpellId, request.Definition.StartingSpellId)
                            .SetProperty(c => c.Status, ContentPublishStatus.Draft)
                            .SetProperty(c => c.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveClassResult.Conflict(
                        await ReadRevisionAsync(db, classId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = classId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.Classes.AsNoTracking()
                        .FirstAsync(c => c.Id == classId, cancellationToken)
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
            return new SaveClassResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveClassResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(FrogDbContext db, ClassEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ClassPublishedSnapshots.Add(new ClassPublishedSnapshotEntity
        {
            Id = snapshotId,
            ClassId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Description = entity.Description,
            BaseHp = entity.BaseHp,
            BaseMp = entity.BaseMp,
            Str = entity.Str,
            Agi = entity.Agi,
            Vit = entity.Vit,
            Int = entity.Int,
            Dex = entity.Dex,
            Luck = entity.Luck,
            StartingSpellId = entity.StartingSpellId,
        });
        db.ClassPublicationHistory.Add(new ClassPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ClassId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.Classes
            .Where(c => c.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(c => c.Status, ContentPublishStatus.Published)
                    .SetProperty(c => c.PublishedRevision, entity.Revision)
                    .SetProperty(c => c.PublishedSnapshotId, snapshotId)
                    .SetProperty(c => c.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredClass?> LoadByIdAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredClass?>(async (db, ct) =>
        {
        var entity = await db.Classes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId, ct)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredClass?> LoadPublishedByIdAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredClass?>(async (db, ct) =>
        {
        var tip = await db.Classes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId, ct)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await db.ClassPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == snapshotId, ct)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        return new StoredClass
        {
            ClassId = snapshot.ClassId,
            Definition = FromSnapshot(snapshot),
            Revision = snapshot.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = snapshot.Revision,
        };
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ClassCatalogEntry>>(async (db, ct) =>
        {
        var query = db.Classes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{value}%")
                || (c.Description != null && EF.Functions.ILike(c.Description, $"%{value}%")));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(c => c.Status == status);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new ClassCatalogEntry
            {
                ClassId = c.Id,
                Name = c.Name,
                BaseHp = c.BaseHp,
                BaseMp = c.BaseMp,
                StartingSpellId = c.StartingSpellId,
                Revision = c.Revision,
                Status = c.Status,
                PublishedRevision = c.PublishedRevision,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteClassResult> DeleteAsync(
        Guid classId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteClassResult>(async (db, ct) =>
        {
        if (!await db.Classes.AsNoTracking().AnyAsync(c => c.Id == classId, ct)
                .ConfigureAwait(false))
        {
            return new DeleteClassResult.NotFound();
        }

        await using var transaction = await db.Database
            .BeginTransactionAsync(ct)
            .ConfigureAwait(false);
        try
        {
            await db.ClassPublicationHistory.Where(h => h.ClassId == classId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.ClassPublishedSnapshots.Where(s => s.ClassId == classId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.Classes.Where(c => c.Id == classId)
                .ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new DeleteClassResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteClassResult.PersistenceFailed(Sanitize(ex.Message));
        }
    
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ClassDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ClassDefinition>>(async (db, ct) =>
        {
        var tips = await db.Classes.AsNoTracking()
            .Where(c => c.PublishedSnapshotId != null)
            .Select(c => c.PublishedSnapshotId!.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (tips.Count == 0)
        {
            return Array.Empty<ClassDefinition>();
        }

        var snapshots = await db.ClassPublishedSnapshots.AsNoTracking()
            .Where(c => tips.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.Classes.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => (long?)c.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ClassEntity ToEntity(
        ClassDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Description = NormalizeDescription(definition.Description),
        BaseHp = definition.BaseHp,
        BaseMp = definition.BaseMp,
        Str = definition.Str,
        Agi = definition.Agi,
        Vit = definition.Vit,
        Int = definition.Int,
        Dex = definition.Dex,
        Luck = definition.Luck,
        StartingSpellId = definition.StartingSpellId,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredClass ToStored(ClassEntity entity) => new()
    {
        ClassId = entity.Id,
        Definition = new ClassDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            BaseHp = entity.BaseHp,
            BaseMp = entity.BaseMp,
            Str = entity.Str,
            Agi = entity.Agi,
            Vit = entity.Vit,
            Int = entity.Int,
            Dex = entity.Dex,
            Luck = entity.Luck,
            StartingSpellId = entity.StartingSpellId,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ClassDefinition FromSnapshot(ClassPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.ClassId,
        Name = snapshot.Name,
        Description = snapshot.Description,
        BaseHp = snapshot.BaseHp,
        BaseMp = snapshot.BaseMp,
        Str = snapshot.Str,
        Agi = snapshot.Agi,
        Vit = snapshot.Vit,
        Int = snapshot.Int,
        Dex = snapshot.Dex,
        Luck = snapshot.Luck,
        StartingSpellId = snapshot.StartingSpellId,
    };

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance de classe.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
