using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresActorRepository : IActorRepository, IPublishedActorCatalog
{
    private readonly FrogDbContextGate _gate;
    private readonly IClassRepository _classes;
    private readonly IPublishedItemCatalog _items;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>Seam de test : appelée après SaveChanges du brouillon, avant commit.</summary>
    internal Func<CancellationToken, Task>? TestBeforeCommitAsync { get; set; }

    public PostgresActorRepository(
        FrogDbContextGate gate,
        IClassRepository? classes = null,
        IPublishedItemCatalog? items = null,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _classes = classes ?? new PostgresClassRepository(gate);
        _items = items ?? new PostgresItemRepository(gate);
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveActorResult> SaveAsync(
        SaveActorRequest request,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<SaveActorResult>(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Definition.Validate(out var error))
            {
                return new SaveActorResult.ValidationFailed(error ?? "Héros invalide.");
            }

            var referenceError = await ValidateReferencesAsync(request.Definition, ct).ConfigureAwait(false);
            if (referenceError is not null)
            {
                return new SaveActorResult.ValidationFailed(referenceError);
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveActorResult.ValidationFailed(
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

    private async Task<SaveActorResult> SaveCoreAsync(
        FrogDbContext db,
        SaveActorRequest request,
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

            if (request.ActorId is not Guid actorId || actorId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveActorResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.Actors.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.Actors
                    .Where(a => a.Id == actorId && a.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(a => a.Revision, request.ExpectedRevision + 1)
                            .SetProperty(a => a.Name, request.Definition.Name.Trim())
                            .SetProperty(a => a.Description, NormalizeOptional(request.Definition.Description))
                            .SetProperty(a => a.ClassId, request.Definition.ClassId)
                            .SetProperty(a => a.FaceLogicalPath, NormalizeOptional(request.Definition.FaceLogicalPath))
                            .SetProperty(a => a.Body, request.Definition.Body)
                            .SetProperty(a => a.Hair, request.Definition.Hair)
                            .SetProperty(a => a.Tunic, request.Definition.Tunic)
                            .SetProperty(a => a.StartingWeaponItemId, request.Definition.StartingWeaponItemId)
                            .SetProperty(a => a.StartingArmorItemId, request.Definition.StartingArmorItemId)
                            .SetProperty(a => a.BaseHp, request.Definition.BaseHp)
                            .SetProperty(a => a.BaseMp, request.Definition.BaseMp)
                            .SetProperty(a => a.Str, request.Definition.Str)
                            .SetProperty(a => a.Agi, request.Definition.Agi)
                            .SetProperty(a => a.Vit, request.Definition.Vit)
                            .SetProperty(a => a.Int, request.Definition.Int)
                            .SetProperty(a => a.Dex, request.Definition.Dex)
                            .SetProperty(a => a.Luck, request.Definition.Luck)
                            .SetProperty(a => a.Status, ContentPublishStatus.Draft)
                            .SetProperty(a => a.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveActorResult.Conflict(
                        await ReadRevisionAsync(db, actorId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = actorId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.Actors.AsNoTracking()
                        .FirstAsync(a => a.Id == actorId, cancellationToken)
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
            return new SaveActorResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveActorResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        ActorEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.ActorPublishedSnapshots.Add(new ActorPublishedSnapshotEntity
        {
            Id = snapshotId,
            ActorId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Description = entity.Description,
            ClassId = entity.ClassId,
            FaceLogicalPath = entity.FaceLogicalPath,
            Body = entity.Body,
            Hair = entity.Hair,
            Tunic = entity.Tunic,
            StartingWeaponItemId = entity.StartingWeaponItemId,
            StartingArmorItemId = entity.StartingArmorItemId,
            BaseHp = entity.BaseHp,
            BaseMp = entity.BaseMp,
            Str = entity.Str,
            Agi = entity.Agi,
            Vit = entity.Vit,
            Int = entity.Int,
            Dex = entity.Dex,
            Luck = entity.Luck,
        });
        db.ActorPublicationHistory.Add(new ActorPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            ActorId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.Actors
            .Where(a => a.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.Status, ContentPublishStatus.Published)
                    .SetProperty(a => a.PublishedRevision, entity.Revision)
                    .SetProperty(a => a.PublishedSnapshotId, snapshotId)
                    .SetProperty(a => a.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredActor?> LoadByIdAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredActor?>(async (db, ct) =>
        {
            var entity = await db.Actors.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == actorId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredActor?> LoadPublishedByIdAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredActor?>(async (db, ct) =>
        {
            var tip = await db.Actors.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == actorId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.ActorPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredActor
            {
                ActorId = snapshot.ActorId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActorCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ActorCatalogEntry>>(async (db, ct) =>
        {
            var query = db.Actors.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(a =>
                    EF.Functions.ILike(a.Name, $"%{value}%")
                    || (a.Description != null && EF.Functions.ILike(a.Description, $"%{value}%"))
                    || (a.FaceLogicalPath != null && EF.Functions.ILike(a.FaceLogicalPath, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(a => a.Status == status);
            }

            return await query
                .OrderBy(a => a.Name)
                .Select(a => new ActorCatalogEntry
                {
                    ActorId = a.Id,
                    Name = a.Name,
                    ClassId = a.ClassId,
                    BaseHp = a.BaseHp,
                    BaseMp = a.BaseMp,
                    Revision = a.Revision,
                    Status = a.Status,
                    PublishedRevision = a.PublishedRevision,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteActorResult> DeleteAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteActorResult>(async (db, ct) =>
        {
            if (!await db.Actors.AsNoTracking().AnyAsync(a => a.Id == actorId, ct).ConfigureAwait(false))
            {
                return new DeleteActorResult.NotFound();
            }

            await using var transaction = await db.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);
            try
            {
                await db.ActorPublicationHistory.Where(h => h.ActorId == actorId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.ActorPublishedSnapshots.Where(s => s.ActorId == actorId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.Actors.Where(a => a.Id == actorId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new DeleteActorResult.Success();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                return new DeleteActorResult.PersistenceFailed(Sanitize(ex.Message));
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActorDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<ActorDefinition>>(async (db, ct) =>
        {
            var tips = await db.Actors.AsNoTracking()
                .Where(a => a.PublishedSnapshotId != null)
                .Select(a => a.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<ActorDefinition>();
            }

            var snapshots = await db.ActorPublishedSnapshots.AsNoTracking()
                .Where(a => tips.Contains(a.Id))
                .OrderBy(a => a.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(FromSnapshot).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ValidateReferencesAsync(
        ActorDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.ClassId is Guid classId
            && await _classes.LoadPublishedByIdAsync(classId, cancellationToken).ConfigureAwait(false) is null)
        {
            return "La classe liée doit exister dans le catalogue publié.";
        }

        if (definition.StartingWeaponItemId is Guid weaponId)
        {
            var weapon = await _items.LoadPublishedByIdAsync(weaponId, cancellationToken).ConfigureAwait(false);
            if (weapon is null || weapon.Kind != ItemType.Weapon)
            {
                return "L’arme de départ doit être un objet publié de type arme.";
            }
        }

        if (definition.StartingArmorItemId is Guid armorId)
        {
            var armor = await _items.LoadPublishedByIdAsync(armorId, cancellationToken).ConfigureAwait(false);
            if (armor is null || armor.Kind != ItemType.Armor)
            {
                return "L’armure de départ doit être un objet publié de type armure.";
            }
        }

        return null;
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.Actors.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => (long?)a.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static ActorEntity ToEntity(ActorDefinition definition, Guid id, DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Description = NormalizeOptional(definition.Description),
        ClassId = definition.ClassId,
        FaceLogicalPath = NormalizeOptional(definition.FaceLogicalPath),
        Body = definition.Body,
        Hair = definition.Hair,
        Tunic = definition.Tunic,
        StartingWeaponItemId = definition.StartingWeaponItemId,
        StartingArmorItemId = definition.StartingArmorItemId,
        BaseHp = definition.BaseHp,
        BaseMp = definition.BaseMp,
        Str = definition.Str,
        Agi = definition.Agi,
        Vit = definition.Vit,
        Int = definition.Int,
        Dex = definition.Dex,
        Luck = definition.Luck,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredActor ToStored(ActorEntity entity) => new()
    {
        ActorId = entity.Id,
        Definition = new ActorDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            ClassId = entity.ClassId,
            FaceLogicalPath = entity.FaceLogicalPath,
            Body = entity.Body,
            Hair = entity.Hair,
            Tunic = entity.Tunic,
            StartingWeaponItemId = entity.StartingWeaponItemId,
            StartingArmorItemId = entity.StartingArmorItemId,
            BaseHp = entity.BaseHp,
            BaseMp = entity.BaseMp,
            Str = entity.Str,
            Agi = entity.Agi,
            Vit = entity.Vit,
            Int = entity.Int,
            Dex = entity.Dex,
            Luck = entity.Luck,
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static ActorDefinition FromSnapshot(ActorPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.ActorId,
        Name = snapshot.Name,
        Description = snapshot.Description,
        ClassId = snapshot.ClassId,
        FaceLogicalPath = snapshot.FaceLogicalPath,
        Body = snapshot.Body,
        Hair = snapshot.Hair,
        Tunic = snapshot.Tunic,
        StartingWeaponItemId = snapshot.StartingWeaponItemId,
        StartingArmorItemId = snapshot.StartingArmorItemId,
        BaseHp = snapshot.BaseHp,
        BaseMp = snapshot.BaseMp,
        Str = snapshot.Str,
        Agi = snapshot.Agi,
        Vit = snapshot.Vit,
        Int = snapshot.Int,
        Dex = snapshot.Dex,
        Luck = snapshot.Luck,
    };

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance de héros.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
