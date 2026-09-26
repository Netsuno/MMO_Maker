using System.Text.Json;
using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresSystemRepository : ISystemRepository, IPublishedSystemCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly FrogDbContextGate _gate;
    private readonly TimeProvider _clock;

    public PostgresSystemRepository(FrogDbContextGate gate, TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public Task<SaveSystemResult> SaveAsync(
        SaveSystemRequest request,
        CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.Definition.Validate(out var error))
            {
                return (SaveSystemResult)new SaveSystemResult.ValidationFailed(error ?? "Document système invalide.");
            }

            return await SaveCoreAsync(db, request, ct).ConfigureAwait(false);
        }, cancellationToken);

    public Task<StoredSystem?> LoadAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var entity = await db.SystemDocuments.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == SystemDefinition.SingletonId, ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken);

    public Task<StoredSystem?> LoadPublishedAsync(CancellationToken cancellationToken = default)
        => _gate.ExecuteAsync(async (db, ct) =>
        {
            var tip = await db.SystemDocuments.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == SystemDefinition.SingletonId, ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.SystemPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredSystem
            {
                Definition = FromJson(snapshot.SystemDocumentId, snapshot.SwitchesJson, snapshot.VariablesJson),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken);

    public async Task<SystemDefinition?> LoadPublishedDefinitionAsync(
        CancellationToken cancellationToken = default)
    {
        var stored = await LoadPublishedAsync(cancellationToken).ConfigureAwait(false);
        return stored?.Definition;
    }

    private async Task<SaveSystemResult> SaveCoreAsync(
        FrogDbContext db,
        SaveSystemRequest request,
        CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var switchesJson = Serialize(request.Definition.Switches);
        var variablesJson = Serialize(request.Definition.Variables);
        await using var transaction = await db.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var existing = await db.SystemDocuments.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == SystemDefinition.SingletonId, cancellationToken)
                .ConfigureAwait(false);

            long newRevision;
            long? publishedRevision;
            if (existing is null)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveSystemResult.Conflict(0);
                }

                var entity = new SystemDocumentEntity
                {
                    Id = SystemDefinition.SingletonId,
                    SwitchesJson = switchesJson,
                    VariablesJson = variablesJson,
                    Status = ContentPublishStatus.Draft,
                    Revision = 1,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                db.SystemDocuments.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                var updatedRows = await db.SystemDocuments
                    .Where(row => row.Id == SystemDefinition.SingletonId && row.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(row => row.Revision, request.ExpectedRevision + 1)
                            .SetProperty(row => row.SwitchesJson, switchesJson)
                            .SetProperty(row => row.VariablesJson, variablesJson)
                            .SetProperty(row => row.Status, ContentPublishStatus.Draft)
                            .SetProperty(row => row.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveSystemResult.Conflict(
                        await ReadRevisionAsync(db, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.SystemDocuments.AsNoTracking()
                        .FirstAsync(row => row.Id == SystemDefinition.SingletonId, cancellationToken)
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
            return new SaveSystemResult.Success(newRevision, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveSystemResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        SystemDocumentEntity entity,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.SystemPublishedSnapshots.Add(new SystemPublishedSnapshotEntity
        {
            Id = snapshotId,
            SystemDocumentId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            SwitchesJson = entity.SwitchesJson,
            VariablesJson = entity.VariablesJson,
        });
        db.SystemPublicationHistory.Add(new SystemPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            SystemDocumentId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.SystemDocuments
            .Where(row => row.Id == entity.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, ContentPublishStatus.Published)
                    .SetProperty(row => row.PublishedRevision, entity.Revision)
                    .SetProperty(row => row.PublishedSnapshotId, snapshotId)
                    .SetProperty(row => row.UpdatedAtUtc, now),
                cancellationToken)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity.Revision;
    }

    private static async Task<long> ReadRevisionAsync(FrogDbContext db, CancellationToken cancellationToken)
    {
        var revision = await db.SystemDocuments.AsNoTracking()
            .Where(row => row.Id == SystemDefinition.SingletonId)
            .Select(row => (long?)row.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static StoredSystem ToStored(SystemDocumentEntity entity) => new()
    {
        Definition = FromJson(entity.Id, entity.SwitchesJson, entity.VariablesJson),
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static SystemDefinition FromJson(Guid id, string switchesJson, string variablesJson) => new()
    {
        Id = id,
        Switches = JsonSerializer.Deserialize<List<SystemSwitchEntry>>(switchesJson, JsonOptions) ?? new(),
        Variables = JsonSerializer.Deserialize<List<SystemVariableEntry>>(variablesJson, JsonOptions) ?? new(),
    };

    private static string Serialize<T>(IReadOnlyList<T> entries)
        => JsonSerializer.Serialize(entries, JsonOptions);

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
