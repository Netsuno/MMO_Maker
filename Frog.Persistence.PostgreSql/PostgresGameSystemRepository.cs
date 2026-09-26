using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresGameSystemRepository :
    IGameSystemRepository,
    IPublishedGameSystemCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate;
    private readonly IPublishedActorCatalog _actors;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresGameSystemRepository(
        FrogDbContextGate gate,
        IPublishedActorCatalog actors,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
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
            if (!request.Definition.Validate(out var error))
            {
                return new SaveGameSystemResult.ValidationFailed(error ?? "Système invalide.");
            }

            var referenceError = await ValidatePartyAsync(request.Definition, ct).ConfigureAwait(false);
            if (referenceError is not null)
            {
                return new SaveGameSystemResult.ValidationFailed(referenceError);
            }

            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveGameSystemResult.ValidationFailed(
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

    private async Task<SaveGameSystemResult> SaveCoreAsync(
        FrogDbContext db,
        SaveGameSystemRequest request,
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
            var switchesJson = SerializeFlags(request.Definition.Switches);
            var variablesJson = SerializeFlags(request.Definition.Variables);
            var partyJson = SerializeParty(request.Definition.StartingPartyActorIds);

            if (request.SystemId is not Guid systemId || systemId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveGameSystemResult.Conflict(0);
                }

                var id = request.Definition.Id == Guid.Empty ? Guid.NewGuid() : request.Definition.Id;
                var entity = ToEntity(request.Definition, switchesJson, variablesJson, partyJson, id, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.GameSystems.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.GameSystems
                    .Where(system => system.Id == systemId && system.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(system => system.Revision, request.ExpectedRevision + 1)
                            .SetProperty(system => system.Name, request.Definition.Name.Trim())
                            .SetProperty(
                                system => system.Description,
                                NormalizeDescription(request.Definition.Description))
                            .SetProperty(system => system.Title, request.Definition.Title.Trim())
                            .SetProperty(system => system.CurrencyUnit, request.Definition.CurrencyUnit.Trim())
                            .SetProperty(system => system.SwitchesJson, switchesJson)
                            .SetProperty(system => system.VariablesJson, variablesJson)
                            .SetProperty(system => system.StartingPartyJson, partyJson)
                            .SetProperty(system => system.Status, ContentPublishStatus.Draft)
                            .SetProperty(system => system.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveGameSystemResult.Conflict(
                        await ReadRevisionAsync(db, systemId, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = systemId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.GameSystems.AsNoTracking()
                        .FirstAsync(system => system.Id == systemId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, entity, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveGameSystemResult.Success(newRevision, savedId, publishedRevision);
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
        GameSystemEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.GameSystemPublishedSnapshots.Add(new GameSystemPublishedSnapshotEntity
        {
            Id = snapshotId,
            GameSystemId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            Name = entity.Name,
            Description = entity.Description,
            Title = entity.Title,
            CurrencyUnit = entity.CurrencyUnit,
            SwitchesJson = entity.SwitchesJson,
            VariablesJson = entity.VariablesJson,
            StartingPartyJson = entity.StartingPartyJson,
        });
        db.GameSystemPublicationHistory.Add(new GameSystemPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            GameSystemId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.GameSystems
            .Where(system => system.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(system => system.Status, ContentPublishStatus.Published)
                    .SetProperty(system => system.PublishedRevision, entity.Revision)
                    .SetProperty(system => system.PublishedSnapshotId, snapshotId)
                    .SetProperty(system => system.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    public async Task<StoredGameSystem?> LoadByIdAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredGameSystem?>(async (db, ct) =>
        {
            var entity = await db.GameSystems.AsNoTracking()
                .FirstOrDefaultAsync(system => system.Id == systemId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredGameSystem?> LoadPublishedByIdAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredGameSystem?>(async (db, ct) =>
        {
            var tip = await db.GameSystems.AsNoTracking()
                .FirstOrDefaultAsync(system => system.Id == systemId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.GameSystemPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(system => system.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredGameSystem
            {
                SystemId = snapshot.GameSystemId,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameSystemCatalogEntry>> ListSummariesAsync(
        string? search = null,
        ContentPublishStatus? statusFilter = null,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<GameSystemCatalogEntry>>(async (db, ct) =>
        {
            var query = db.GameSystems.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                query = query.Where(system =>
                    EF.Functions.ILike(system.Name, $"%{value}%")
                    || EF.Functions.ILike(system.Title, $"%{value}%")
                    || EF.Functions.ILike(system.CurrencyUnit, $"%{value}%")
                    || (system.Description != null && EF.Functions.ILike(system.Description, $"%{value}%")));
            }

            if (statusFilter is { } status)
            {
                query = query.Where(system => system.Status == status);
            }

            var rows = await query
                .OrderBy(system => system.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return rows.Select(system => new GameSystemCatalogEntry
            {
                SystemId = system.Id,
                Name = system.Name,
                Title = system.Title,
                CurrencyUnit = system.CurrencyUnit,
                SwitchCount = DeserializeFlags(system.SwitchesJson).Count,
                VariableCount = DeserializeFlags(system.VariablesJson).Count,
                PartyCount = DeserializeParty(system.StartingPartyJson).Count,
                Revision = system.Revision,
                Status = system.Status,
                PublishedRevision = system.PublishedRevision,
            }).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeleteGameSystemResult> DeleteAsync(
        Guid systemId,
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<DeleteGameSystemResult>(async (db, ct) =>
        {
            if (!await db.GameSystems.AsNoTracking().AnyAsync(system => system.Id == systemId, ct)
                    .ConfigureAwait(false))
            {
                return new DeleteGameSystemResult.NotFound();
            }

            await using var transaction = await db.Database
                .BeginTransactionAsync(ct)
                .ConfigureAwait(false);
            try
            {
                await db.GameSystemPublicationHistory.Where(history => history.GameSystemId == systemId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.GameSystemPublishedSnapshots.Where(snapshot => snapshot.GameSystemId == systemId)
                    .ExecuteDeleteAsync(ct).ConfigureAwait(false);
                await db.GameSystems.Where(system => system.Id == systemId)
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

    public async Task<IReadOnlyList<GameSystemDefinition>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<IReadOnlyList<GameSystemDefinition>>(async (db, ct) =>
        {
            var tips = await db.GameSystems.AsNoTracking()
                .Where(system => system.PublishedSnapshotId != null)
                .Select(system => system.PublishedSnapshotId!.Value)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (tips.Count == 0)
            {
                return Array.Empty<GameSystemDefinition>();
            }

            var snapshots = await db.GameSystemPublishedSnapshots.AsNoTracking()
                .Where(snapshot => tips.Contains(snapshot.Id))
                .OrderBy(snapshot => snapshot.Name)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            return snapshots.Select(FromSnapshot).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> ValidatePartyAsync(
        GameSystemDefinition definition,
        CancellationToken cancellationToken)
    {
        if (definition.StartingPartyActorIds.Count == 0)
        {
            return null;
        }

        var published = await _actors.ListPublishedAsync(cancellationToken).ConfigureAwait(false);
        var known = published.Select(actor => actor.Id).ToHashSet();
        foreach (var actorId in definition.StartingPartyActorIds)
        {
            if (!known.Contains(actorId))
            {
                return $"Le héros {actorId:N} du groupe de départ doit exister dans le catalogue publié.";
            }
        }

        return null;
    }

    private async Task<long> ReadRevisionAsync(FrogDbContext db, Guid id, CancellationToken cancellationToken)
    {
        var revision = await db.GameSystems.AsNoTracking()
            .Where(system => system.Id == id)
            .Select(system => (long?)system.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static GameSystemEntity ToEntity(
        GameSystemDefinition definition,
        string switchesJson,
        string variablesJson,
        string partyJson,
        Guid id,
        DateTimeOffset now) => new()
    {
        Id = id,
        Name = definition.Name.Trim(),
        Description = NormalizeDescription(definition.Description),
        Title = definition.Title.Trim(),
        CurrencyUnit = definition.CurrencyUnit.Trim(),
        SwitchesJson = switchesJson,
        VariablesJson = variablesJson,
        StartingPartyJson = partyJson,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredGameSystem ToStored(GameSystemEntity entity) => new()
    {
        SystemId = entity.Id,
        Definition = new GameSystemDefinition
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Title = entity.Title,
            CurrencyUnit = entity.CurrencyUnit,
            Switches = DeserializeFlags(entity.SwitchesJson),
            Variables = DeserializeFlags(entity.VariablesJson),
            StartingPartyActorIds = DeserializeParty(entity.StartingPartyJson),
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static GameSystemDefinition FromSnapshot(GameSystemPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.GameSystemId,
        Name = snapshot.Name,
        Description = snapshot.Description,
        Title = snapshot.Title,
        CurrencyUnit = snapshot.CurrencyUnit,
        Switches = DeserializeFlags(snapshot.SwitchesJson),
        Variables = DeserializeFlags(snapshot.VariablesJson),
        StartingPartyActorIds = DeserializeParty(snapshot.StartingPartyJson),
    };

    private static string SerializeFlags(IReadOnlyList<NamedWorldFlag> flags)
        => JsonSerializer.Serialize(flags, JsonOptions);

    private static List<NamedWorldFlag> DeserializeFlags(string json)
        => JsonSerializer.Deserialize<List<NamedWorldFlag>>(json, JsonOptions) ?? new List<NamedWorldFlag>();

    private static string SerializeParty(IReadOnlyList<Guid> actorIds)
        => JsonSerializer.Serialize(actorIds, JsonOptions);

    private static List<Guid> DeserializeParty(string json)
        => JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new List<Guid>();

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

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
