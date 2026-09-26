using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.PostgreSql;

public sealed class PostgresSystemSettingsRepository : ISystemSettingsRepository, IPublishedSystemSettings
{
    private readonly FrogDbContextGate _gate;
    private readonly IPublishedActorCatalog? _actors;
    private readonly IMapRepository? _maps;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public PostgresSystemSettingsRepository(
        FrogDbContextGate gate,
        IPublishedActorCatalog? actors = null,
        IMapRepository? maps = null,
        TimeProvider? clock = null)
    {
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _actors = actors;
        _maps = maps;
        _clock = clock ?? TimeProvider.System;
    }

    public ContentRepositoryCapabilities Capabilities => ContentRepositoryCapabilities.PostgreSql;

    public async Task<SaveSystemSettingsResult> SaveAsync(
        SaveSystemSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SystemDefinition.TryCanonicalize(request.Definition, out var definition, out var error))
        {
            return new SaveSystemSettingsResult.ValidationFailed(error ?? "Paramètres système invalides.");
        }

        var referenceError = await SystemSettingsReferenceChecks
            .ValidateAsync(definition, _actors, _maps, cancellationToken)
            .ConfigureAwait(false);
        if (referenceError is not null)
        {
            return new SaveSystemSettingsResult.ValidationFailed(referenceError);
        }

        return await _gate.ExecuteAsync<SaveSystemSettingsResult>(async (db, ct) =>
        {
            if (!await _saveGate.WaitAsync(0, ct).ConfigureAwait(false))
            {
                return new SaveSystemSettingsResult.ValidationFailed(
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

    private async Task<SaveSystemSettingsResult> SaveCoreAsync(
        FrogDbContext db,
        SaveSystemSettingsRequest request,
        SystemDefinition definition,
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

            if (request.SettingsId is not Guid settingsId || settingsId == Guid.Empty)
            {
                if (request.ExpectedRevision != 0)
                {
                    return new SaveSystemSettingsResult.Conflict(0);
                }

                if (await db.SystemSettings.AsNoTracking().AnyAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new SaveSystemSettingsResult.Conflict(
                        await ReadRevisionAsync(db, cancellationToken).ConfigureAwait(false));
                }

                var entity = ToEntity(definition, now);
                entity.Revision = 1;
                entity.Status = ContentPublishStatus.Draft;
                db.SystemSettings.Add(entity);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                newRevision = 1;
                savedId = entity.Id;
                publishedRevision = request.Intent == SaveContentIntent.Publish
                    ? await PublishSnapshotAsync(db, entity, definition, now, cancellationToken).ConfigureAwait(false)
                    : null;
            }
            else
            {
                if (settingsId != SystemDefinition.SingletonId)
                {
                    return new SaveSystemSettingsResult.ValidationFailed("Le document Système est unique.");
                }

                var updatedRows = await db.SystemSettings
                    .Where(row => row.Id == settingsId && row.Revision == request.ExpectedRevision)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(row => row.Revision, request.ExpectedRevision + 1)
                            .SetProperty(row => row.CurrencyUnit, definition.CurrencyUnit)
                            .SetProperty(row => row.TermHp, definition.TermHp)
                            .SetProperty(row => row.TermMp, definition.TermMp)
                            .SetProperty(row => row.PartyActor1, definition.PartyActor1)
                            .SetProperty(row => row.PartyActor2, definition.PartyActor2)
                            .SetProperty(row => row.PartyActor3, definition.PartyActor3)
                            .SetProperty(row => row.PartyActor4, definition.PartyActor4)
                            .SetProperty(row => row.StartMapId, definition.StartMapId)
                            .SetProperty(row => row.TitleBgmAsset, definition.TitleBgm.Asset)
                            .SetProperty(row => row.TitleBgmVolume, definition.TitleBgm.Volume)
                            .SetProperty(row => row.TitleBgmFadeMs, definition.TitleBgm.FadeMs)
                            .SetProperty(row => row.StartBgmAsset, definition.StartBgm.Asset)
                            .SetProperty(row => row.StartBgmVolume, definition.StartBgm.Volume)
                            .SetProperty(row => row.StartBgmFadeMs, definition.StartBgm.FadeMs)
                            .SetProperty(row => row.Status, ContentPublishStatus.Draft)
                            .SetProperty(row => row.UpdatedAtUtc, now),
                        cancellationToken)
                    .ConfigureAwait(false);

                if (updatedRows == 0)
                {
                    return new SaveSystemSettingsResult.Conflict(
                        await ReadRevisionAsync(db, cancellationToken).ConfigureAwait(false));
                }

                newRevision = request.ExpectedRevision + 1;
                savedId = settingsId;
                if (request.Intent == SaveContentIntent.Publish)
                {
                    var entity = await db.SystemSettings.AsNoTracking()
                        .FirstAsync(row => row.Id == settingsId, cancellationToken)
                        .ConfigureAwait(false);
                    publishedRevision = await PublishSnapshotAsync(db, entity, definition, now, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    publishedRevision = null;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveSystemSettingsResult.Success(newRevision, savedId, publishedRevision);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            db.ChangeTracker.Clear();
            return new SaveSystemSettingsResult.PersistenceFailed(Sanitize(ex.Message));
        }
    }

    private async Task<long> PublishSnapshotAsync(
        FrogDbContext db,
        SystemSettingsEntity entity,
        SystemDefinition definition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var snapshotId = Guid.NewGuid();
        db.SystemSettingsPublishedSnapshots.Add(new SystemSettingsPublishedSnapshotEntity
        {
            Id = snapshotId,
            SettingsId = entity.Id,
            Revision = entity.Revision,
            PublishedAtUtc = now,
            CurrencyUnit = definition.CurrencyUnit,
            TermHp = definition.TermHp,
            TermMp = definition.TermMp,
            PartyActor1 = definition.PartyActor1,
            PartyActor2 = definition.PartyActor2,
            PartyActor3 = definition.PartyActor3,
            PartyActor4 = definition.PartyActor4,
            StartMapId = definition.StartMapId,
            TitleBgmAsset = definition.TitleBgm.Asset,
            TitleBgmVolume = definition.TitleBgm.Volume,
            TitleBgmFadeMs = definition.TitleBgm.FadeMs,
            StartBgmAsset = definition.StartBgm.Asset,
            StartBgmVolume = definition.StartBgm.Volume,
            StartBgmFadeMs = definition.StartBgm.FadeMs,
        });
        db.SystemSettingsPublicationHistory.Add(new SystemSettingsPublicationHistoryEntity
        {
            Id = Guid.NewGuid(),
            SettingsId = entity.Id,
            SnapshotId = snapshotId,
            Revision = entity.Revision,
            PublishedAtUtc = now,
        });

        await db.SystemSettings
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

    public async Task<StoredSystemSettings?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredSystemSettings?>(async (db, ct) =>
        {
            var entity = await db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            return entity is null ? null : ToStored(entity);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredSystemSettings?> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await _gate.ExecuteAsync<StoredSystemSettings?>(async (db, ct) =>
        {
            var tip = await db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (tip?.PublishedSnapshotId is not Guid snapshotId)
            {
                return null;
            }

            var snapshot = await db.SystemSettingsPublishedSnapshots.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == snapshotId, ct)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            return new StoredSystemSettings
            {
                SettingsId = tip.Id,
                Definition = FromSnapshot(snapshot),
                Revision = snapshot.Revision,
                Status = ContentPublishStatus.Published,
                PublishedRevision = snapshot.Revision,
            };
        }, cancellationToken).ConfigureAwait(false);
    }

    async Task<SystemDefinition?> IPublishedSystemSettings.LoadPublishedAsync(CancellationToken cancellationToken)
    {
        var stored = await LoadPublishedAsync(cancellationToken).ConfigureAwait(false);
        return stored?.Definition;
    }

    private static async Task<long> ReadRevisionAsync(FrogDbContext db, CancellationToken cancellationToken)
    {
        var revision = await db.SystemSettings.AsNoTracking()
            .Select(row => (long?)row.Revision)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return revision ?? 0;
    }

    private static SystemSettingsEntity ToEntity(SystemDefinition definition, DateTimeOffset now) => new()
    {
        Id = SystemDefinition.SingletonId,
        CurrencyUnit = definition.CurrencyUnit,
        TermHp = definition.TermHp,
        TermMp = definition.TermMp,
        PartyActor1 = definition.PartyActor1,
        PartyActor2 = definition.PartyActor2,
        PartyActor3 = definition.PartyActor3,
        PartyActor4 = definition.PartyActor4,
        StartMapId = definition.StartMapId,
        TitleBgmAsset = definition.TitleBgm.Asset,
        TitleBgmVolume = definition.TitleBgm.Volume,
        TitleBgmFadeMs = definition.TitleBgm.FadeMs,
        StartBgmAsset = definition.StartBgm.Asset,
        StartBgmVolume = definition.StartBgm.Volume,
        StartBgmFadeMs = definition.StartBgm.FadeMs,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };

    private static StoredSystemSettings ToStored(SystemSettingsEntity entity) => new()
    {
        SettingsId = entity.Id,
        Definition = new SystemDefinition
        {
            Id = entity.Id,
            CurrencyUnit = entity.CurrencyUnit,
            TermHp = entity.TermHp,
            TermMp = entity.TermMp,
            PartyActor1 = entity.PartyActor1,
            PartyActor2 = entity.PartyActor2,
            PartyActor3 = entity.PartyActor3,
            PartyActor4 = entity.PartyActor4,
            StartMapId = entity.StartMapId,
            TitleBgm = new MapAudioTrack
            {
                Asset = entity.TitleBgmAsset,
                Volume = entity.TitleBgmVolume,
                FadeMs = entity.TitleBgmFadeMs,
            },
            StartBgm = new MapAudioTrack
            {
                Asset = entity.StartBgmAsset,
                Volume = entity.StartBgmVolume,
                FadeMs = entity.StartBgmFadeMs,
            },
        },
        Revision = entity.Revision,
        Status = entity.Status,
        PublishedRevision = entity.PublishedRevision,
    };

    private static SystemDefinition FromSnapshot(SystemSettingsPublishedSnapshotEntity snapshot) => new()
    {
        Id = snapshot.SettingsId,
        CurrencyUnit = snapshot.CurrencyUnit,
        TermHp = snapshot.TermHp,
        TermMp = snapshot.TermMp,
        PartyActor1 = snapshot.PartyActor1,
        PartyActor2 = snapshot.PartyActor2,
        PartyActor3 = snapshot.PartyActor3,
        PartyActor4 = snapshot.PartyActor4,
        StartMapId = snapshot.StartMapId,
        TitleBgm = new MapAudioTrack
        {
            Asset = snapshot.TitleBgmAsset,
            Volume = snapshot.TitleBgmVolume,
            FadeMs = snapshot.TitleBgmFadeMs,
        },
        StartBgm = new MapAudioTrack
        {
            Asset = snapshot.StartBgmAsset,
            Volume = snapshot.StartBgmVolume,
            FadeMs = snapshot.StartBgmFadeMs,
        },
    };

    private static string Sanitize(string message)
    {
        if (message.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Connection String", StringComparison.OrdinalIgnoreCase))
        {
            return "Échec de persistance des paramètres système.";
        }

        return message.Length > 200 ? message[..200] : message;
    }
}
