using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresSpellRepository : ISpellRepository, IPublishedSpellCatalog
{
    private readonly FrogDbContext _db;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresSpellRepository(FrogDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveSpellResult> SaveAsync(
        SaveSpellRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Definition.Validate(out var error))
        {
            return new SaveSpellResult.ValidationFailed(error ?? "Sort/compétence invalide.");
        }

        if (!await _saveGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new SaveSpellResult.ValidationFailed(
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

    private async Task<SaveSpellResult> SaveCoreAsync(
        SaveSpellRequest request,
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

            if (request.SpellId is not Guid spellId || spellId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveSpellResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                _db.Spells.Add(entity);
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await _db.Spells
                    .Where(s => s.Id == spellId && s.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(s => s.Revision, request.ExpectedRevision + 1)
                            .SetProperty(s => s.Name, request.Definition.Name.Trim())
                            .SetProperty(s => s.Kind, request.Definition.Kind)
                            .SetProperty(s => s.ManaCost, request.Definition.ManaCost)
                            .SetProperty(s => s.CooldownMs, request.Definition.CooldownMs)
                            .SetProperty(s => s.TargetType, request.Definition.TargetType)
                            .SetProperty(
                                s => s.IconLogicalPath,
                                request.Definition.IconLogicalPath.Trim().Replace('\\', '/'))
                            .SetProperty(
                                s => s.Description,
                                NormalizeDescription(request.Definition.Description))
                            .SetProperty(s => s.Status, ContentPublishStatus.Draft)
                            .SetProperty(s => s.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveSpellResult.Conflict(
                        await ReadRevisionAsync(spellId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = spellId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await _db.Spells.AsNoTracking()
                        .FirstAsync(s => s.Id == spellId, cancellationToken)
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
            return new SaveSpellResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            _db.ChangeTracker.Clear();
            return new SaveSpellResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        SpellEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        _db.SpellPublishedSnapshots.Add(new SpellPublishedSnapshotEntity
        {
            Id = snapshotId,
            SpellId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Kind = entity.Kind,
            ManaCost = entity.ManaCost,
            CooldownMs = entity.CooldownMs,
            TargetType = entity.TargetType,
            IconLogicalPath = entity.IconLogicalPath,
            Description = entity.Description,
        });
        _db.SpellPublicationHistory.Add(new SpellPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            SpellId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await _db.Spells
            .Where(s => s.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Status, ContentPublishStatus.Published)
                    .SetProperty(s => s.PublishedRevision, entity.Revision)
                    .SetProperty(s => s.PublishedSnapshotId, snapshotId)
                    .SetProperty(s => s.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredSpell?> LoadByIdAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Spells.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spellId, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToStored(entity);
    }

    public async Task<StoredSpell?> LoadPublishedByIdAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        var tip = await _db.Spells.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == spellId, cancellationToken)
            .ConfigureAwait(false);
        if (tip?.PublishedSnapshotId is not Guid snapshotId)
        {
            return null;
        }

        var snapshot = await _db.SpellPublishedSnapshots.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId, cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return null;
        }

        return new StoredSpell
        {
            SpellId = snapshot.SpellId,
            Definition = FromSnapshot(snapshot),
            Revision = snapshot.Revision,
            Status = ContentPublishStatus.Published,
            PublishedRevision = snapshot.Revision,
        };
    }

    public async Task<IReadOnlyList<SpellCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Spells.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, $"%{value}%")
                || EF.Functions.ILike(s.IconLogicalPath, $"%{value}%")
                || (s.Description != null && EF.Functions.ILike(s.Description, $"%{value}%")));
        }

        if (statusFilter is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SpellCatalogEntry
            {
                SpellId = s.Id,
                Name = s.Name,
                Kind = s.Kind,
                ManaCost = s.ManaCost,
                CooldownMs = s.CooldownMs,
                TargetType = s.TargetType,
                IconLogicalPath = s.IconLogicalPath,
                Revision = s.Revision,
                Status = s.Status,
                PublishedRevision = s.PublishedRevision,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeleteSpellResult> DeleteAsync(
        Guid spellId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.Spells.AsNoTracking().AnyAsync(s => s.Id == spellId, cancellationToken)
                .ConfigureAwait(false))
        {
            return new DeleteSpellResult.NotFound();
        }

        await using var transaction = await _db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            await _db.SpellPublicationHistory.Where(h => h.SpellId == spellId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.SpellPublishedSnapshots.Where(s => s.SpellId == spellId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await _db.Spells.Where(s => s.Id == spellId)
                .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new DeleteSpellResult.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            return new DeleteSpellResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    public async Task<IReadOnlyList<SpellDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var tips = await _db.Spells.AsNoTracking()
            .Where(s => s.PublishedSnapshotId != null)
            .Select(s => s.PublishedSnapshotId!.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tips.Count == 0)
        {
            return Array.Empty<SpellDefinition>();
        }

        var snapshots = await _db.SpellPublishedSnapshots.AsNoTracking()
            .Where(s => tips.Contains(s.Id))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return snapshots.Select(FromSnapshot).ToList();
    }

    private async Task<long> ReadRevisionAsync(Guid id, CancellationToken cancellationToken)
    {
        var revision = await _db.Spells.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => (long?)s.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static SpellEntity ToEntity(
        SpellDefinition definition,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Kind = definition.Kind,
        ManaCost = definition.ManaCost,
        CooldownMs = definition.CooldownMs,
        TargetType = definition.TargetType,
        IconLogicalPath = definition.IconLogicalPath.Trim().Replace('\\', '/'),
        Description = NormalizeDescription(definition.Description),
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredSpell ToStored(SpellEntity entity) => new()
    {
        SpellId = entity.Id,
        Definition = new SpellDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Kind = entity.Kind,
            ManaCost = entity.ManaCost,
            CooldownMs = entity.CooldownMs,
            TargetType = entity.TargetType,
            IconLogicalPath = entity.IconLogicalPath,
            Description = entity.Description,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static SpellDefinition FromSnapshot(SpellPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.SpellId,
        Name = snapshot.Name,
        Kind = snapshot.Kind,
        ManaCost = snapshot.ManaCost,
        CooldownMs = snapshot.CooldownMs,
        TargetType = snapshot.TargetType,
        IconLogicalPath = snapshot.IconLogicalPath,
        Description = snapshot.Description,
    };

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance sort/compétence.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
