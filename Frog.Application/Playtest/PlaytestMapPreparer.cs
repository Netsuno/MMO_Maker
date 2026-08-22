using Frog.Application.Maps;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>
/// Prépare un playtest : valide, enregistre si sale, publie, charge UNIQUEMENT le snapshot publié,
/// alloue les ids runtime et écrit le manifeste (jamais un brouillon).
/// </summary>
public interface IPlaytestMapPreparer
{
    Task<PlaytestPreparationResult> PrepareAsync(
        MapWorkspaceSession workspace,
        PlaytestPrepareRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PlaytestPrepareRequest
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 0;
    public int SpawnTileX { get; init; }
    public int SpawnTileY { get; init; }
    public string? WorkDirectory { get; init; }
    /// <summary>Si true (défaut), refuse les dépôts non durables (PostgreSQL requis en éditeur).</summary>
    public bool RequireDurablePersistence { get; init; } = true;
    /// <summary>Si true, republie même si une révision publiée existe déjà (après save brouillon).</summary>
    public bool PublishCurrentBeforeLaunch { get; init; } = true;
}

public sealed class PlaytestMapPreparer : IPlaytestMapPreparer
{
    private readonly IMapRepository _repository;
    private readonly MapSerializer _serializer = new();

    public PlaytestMapPreparer(IMapRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<PlaytestPreparationResult> PrepareAsync(
        MapWorkspaceSession workspace,
        PlaytestPrepareRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!workspace.Capabilities.AllowsSave)
        {
            return new PlaytestPreparationResult.Failed(
                "Playtest impossible : la session n’autorise pas l’enregistrement. Configurez PostgreSQL durable.",
                PlaytestFailureKind.NotDurable);
        }

        if (request.RequireDurablePersistence && !workspace.Capabilities.IsDurablePersistence)
        {
            return new PlaytestPreparationResult.Failed(
                "Playtest impossible : PostgreSQL durable requis (les brouillons mémoire ne sont pas playtestables).",
                PlaytestFailureKind.NotDurable);
        }

        if (workspace.CurrentMap is null || workspace.CurrentMapId is not Guid mapId || mapId == Guid.Empty)
        {
            return new PlaytestPreparationResult.Failed(
                "Aucune carte ouverte pour le playtest.",
                PlaytestFailureKind.Validation);
        }

        if (!workspace.CurrentMap.Validate(out var validateError))
        {
            return new PlaytestPreparationResult.Failed(
                validateError ?? "Carte invalide.",
                PlaytestFailureKind.Validation);
        }

        // Jamais playtester des changements uniquement en mémoire.
        if (workspace.IsDirty)
        {
            var draftSave = await workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft, cancellationToken)
                .ConfigureAwait(false);
            if (draftSave is not SaveMapResult.Success)
            {
                return MapSaveFailure(draftSave, "Impossible d’enregistrer le brouillon avant playtest.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        long publishedRevision;
        if (request.PublishCurrentBeforeLaunch || workspace.PublishedRevision is null)
        {
            var publish = await workspace.SaveCurrentAsync(SaveMapIntent.Publish, cancellationToken)
                .ConfigureAwait(false);
            if (publish is not SaveMapResult.Success success)
            {
                return MapSaveFailure(publish, "Impossible de publier la carte avant playtest.");
            }

            if (success.PublishedRevision is not long pubRev)
            {
                return new PlaytestPreparationResult.Failed(
                    "Publication sans révision publiée retournée.",
                    PlaytestFailureKind.NotPublished);
            }

            publishedRevision = pubRev;
        }
        else
        {
            publishedRevision = workspace.PublishedRevision.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var primaryPublished = await _repository
            .LoadPublishedByIdAndRevisionAsync(mapId, publishedRevision, cancellationToken)
            .ConfigureAwait(false);
        if (primaryPublished is null)
        {
            return new PlaytestPreparationResult.Failed(
                $"Snapshot publié introuvable pour MapId={mapId} révision={publishedRevision}.",
                PlaytestFailureKind.MissingPublishedRevision);
        }

        // Preuve anti-brouillon : la révision chargée doit être celle publiée, pas le brouillon courant.
        if (primaryPublished.Revision != publishedRevision ||
            primaryPublished.Status != MapPublishStatus.Published)
        {
            return new PlaytestPreparationResult.Failed(
                "Le dépôt a renvoyé un état non publié pour le playtest.",
                PlaytestFailureKind.NotPublished);
        }

        var allocator = new RuntimeMapIdAllocator();
        var primaryRuntimeId = allocator.Allocate(mapId);

        var closureIds = CollectWarpTargetIds(primaryPublished.Map);
        closureIds.Remove(mapId);

        var publishedMaps = new Dictionary<Guid, StoredMap> { [mapId] = primaryPublished };
        foreach (var targetId in closureIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = await _repository.LoadPublishedByIdAsync(targetId, cancellationToken)
                .ConfigureAwait(false);
            if (target is null)
            {
                return new PlaytestPreparationResult.Failed(
                    $"Warp vers MapId={targetId} : aucune révision publiée (brouillon non exposé).",
                    PlaytestFailureKind.NotPublished);
            }

            publishedMaps[targetId] = target;
            allocator.Allocate(targetId);
        }

        var runtimeMaps = new List<PlaytestRuntimeMap>();
        foreach (var kvp in publishedMaps.OrderBy(k =>
                     allocator.TryGetRuntimeId(k.Key, out var runtimeOrdered) ? runtimeOrdered : int.MaxValue))
        {
            var canonicalId = kvp.Key;
            var stored = kvp.Value;
            var rewritten = allocator.RewriteWarpsToRuntimeGuids(stored.Map);
            var bytes = _serializer.Serialize(rewritten);
            if (!allocator.TryGetRuntimeId(canonicalId, out var runtimeId))
            {
                return new PlaytestPreparationResult.Failed(
                    "Allocation runtime incohérente.",
                    PlaytestFailureKind.Validation);
            }

            runtimeMaps.Add(new PlaytestRuntimeMap
            {
                CanonicalMapId = canonicalId,
                PublishedRevision = stored.Revision,
                RuntimeMapId = runtimeId,
                Name = rewritten.Name,
                Map = rewritten,
                SerializedFmap = bytes,
            });
        }

        if (!primaryPublished.Map.Validate(out _))
        {
            return new PlaytestPreparationResult.Failed(
                "Snapshot publié invalide.",
                PlaytestFailureKind.Validation);
        }

        var spawnX = request.SpawnTileX;
        var spawnY = request.SpawnTileY;
        if (spawnX < 0 || spawnY < 0 ||
            spawnX >= primaryPublished.Map.Width ||
            spawnY >= primaryPublished.Map.Height)
        {
            return new PlaytestPreparationResult.Failed(
                $"Position de spawn hors carte ({spawnX},{spawnY}).",
                PlaytestFailureKind.Validation);
        }

        var workDir = request.WorkDirectory
                      ?? Path.Combine(Path.GetTempPath(), "frog-playtest", request.CorrelationId.ToString("N"));
        Directory.CreateDirectory(workDir);
        var manifestPath = Path.Combine(workDir, "playtest-manifest.json");

        var port = request.Port > 0 ? request.Port : 0;
        var plan = new PlaytestLaunchPlan
        {
            CorrelationId = request.CorrelationId,
            PrimaryCanonicalMapId = mapId,
            PrimaryPublishedRevision = publishedRevision,
            Spawn = new PlaytestSpawnPoint
            {
                RuntimeMapId = primaryRuntimeId,
                TileX = spawnX,
                TileY = spawnY,
            },
            Maps = runtimeMaps,
            Host = request.Host,
            Port = port,
            WorkDirectory = workDir,
            ManifestPath = manifestPath,
        };

        PlaytestManifestWriter.Write(plan);
        return new PlaytestPreparationResult.Success(plan);
    }

    private static HashSet<Guid> CollectWarpTargetIds(Map map)
    {
        var ids = new HashSet<Guid>();
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.WarpTargetMapId != Guid.Empty)
                {
                    ids.Add(tile.WarpTargetMapId);
                }
            }
        }

        return ids;
    }

    private static PlaytestPreparationResult MapSaveFailure(SaveMapResult result, string fallback)
        => result switch
        {
            SaveMapResult.ValidationFailed v => new PlaytestPreparationResult.Failed(
                v.Error, PlaytestFailureKind.Validation),
            SaveMapResult.Conflict c => new PlaytestPreparationResult.Failed(
                $"Conflit de révision (courante={c.CurrentRevision}). Rechargez puis réessayez.",
                PlaytestFailureKind.Persistence),
            SaveMapResult.PersistenceFailed p => new PlaytestPreparationResult.Failed(
                p.Error, PlaytestFailureKind.Persistence),
            SaveMapResult.NotDurable n => new PlaytestPreparationResult.Failed(
                n.Message, PlaytestFailureKind.NotDurable),
            _ => new PlaytestPreparationResult.Failed(fallback, PlaytestFailureKind.Persistence),
        };
}
