using Frog.Application.Maps;
using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>
/// Prépare un playtest : valide → save (nouvelle ou dirty) → publish → charge snapshot publié
/// + graphe de warps transitif (BFS) — jamais un brouillon.
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
    public bool RequireDurablePersistence { get; init; } = true;
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

        if (workspace.CurrentMap is null)
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

        if (!PlaytestSpawnValidator.TryValidate(
                workspace.CurrentMap,
                request.SpawnTileX,
                request.SpawnTileY,
                out var spawnError))
        {
            return new PlaytestPreparationResult.Failed(
                spawnError ?? "Spawn invalide.",
                PlaytestFailureKind.Validation);
        }

        // Nouvelle carte (pas d’ID) ou dirty → enregistrer pour obtenir MapId / révision.
        if (workspace.CurrentMapId is null || workspace.CurrentMapId == Guid.Empty || workspace.IsDirty)
        {
            var draftSave = await workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft, cancellationToken)
                .ConfigureAwait(false);
            if (draftSave is not SaveMapResult.Success)
            {
                return MapSaveFailure(draftSave, "Impossible d’enregistrer la carte avant playtest.");
            }
        }

        if (workspace.CurrentMapId is not Guid mapId || mapId == Guid.Empty)
        {
            return new PlaytestPreparationResult.Failed(
                "MapId manquant après enregistrement.",
                PlaytestFailureKind.Persistence);
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

            mapId = success.MapId;
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

        if (primaryPublished.Revision != publishedRevision ||
            primaryPublished.Status != MapPublishStatus.Published)
        {
            return new PlaytestPreparationResult.Failed(
                "Le dépôt a renvoyé un état non publié pour le playtest.",
                PlaytestFailureKind.NotPublished);
        }

        if (!PlaytestSpawnValidator.TryValidate(
                primaryPublished.Map,
                request.SpawnTileX,
                request.SpawnTileY,
                out var pubSpawnError))
        {
            return new PlaytestPreparationResult.Failed(
                pubSpawnError ?? "Spawn invalide sur le snapshot publié.",
                PlaytestFailureKind.Validation);
        }

        var loadResult = await TryLoadPublishedWarpClosureAsync(mapId, primaryPublished, cancellationToken)
            .ConfigureAwait(false);
        if (!loadResult.Ok)
        {
            return new PlaytestPreparationResult.Failed(loadResult.Error!, loadResult.Kind);
        }

        var allocator = new RuntimeMapIdAllocator();
        var primaryRuntimeId = allocator.Allocate(mapId);
        foreach (var id in loadResult.Maps!.Keys.Where(k => k != mapId).OrderBy(k => k))
        {
            allocator.Allocate(id);
        }

        var runtimeMaps = new List<PlaytestRuntimeMap>();
        foreach (var kvp in loadResult.Maps.OrderBy(k =>
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

        var workDir = request.WorkDirectory
                      ?? Path.Combine(Path.GetTempPath(), "frog-playtest", request.CorrelationId.ToString("N"));
        Directory.CreateDirectory(workDir);
        var manifestPath = Path.Combine(workDir, "playtest-manifest.json");

        var plan = new PlaytestLaunchPlan
        {
            CorrelationId = request.CorrelationId,
            PrimaryCanonicalMapId = mapId,
            PrimaryPublishedRevision = publishedRevision,
            Spawn = new PlaytestSpawnPoint
            {
                RuntimeMapId = primaryRuntimeId,
                TileX = request.SpawnTileX,
                TileY = request.SpawnTileY,
            },
            Maps = runtimeMaps,
            Host = string.IsNullOrWhiteSpace(request.Host) ? "127.0.0.1" : request.Host,
            Port = request.Port > 0 ? request.Port : 0,
            WorkDirectory = workDir,
            ManifestPath = manifestPath,
        };

        PlaytestManifestWriter.Write(plan);
        return new PlaytestPreparationResult.Success(plan);
    }

    private async Task<(bool Ok, string? Error, PlaytestFailureKind Kind, Dictionary<Guid, StoredMap>? Maps)>
        TryLoadPublishedWarpClosureAsync(
            Guid primaryId,
            StoredMap primary,
            CancellationToken cancellationToken)
    {
        var maps = new Dictionary<Guid, StoredMap> { [primaryId] = primary };
        var queue = new Queue<Guid>();
        queue.Enqueue(primaryId);
        var visited = new HashSet<Guid> { primaryId };

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentId = queue.Dequeue();
            foreach (var targetId in CollectDirectWarpTargetIds(maps[currentId].Map))
            {
                if (!visited.Add(targetId))
                {
                    continue;
                }

                var target = await _repository.LoadPublishedByIdAsync(targetId, cancellationToken)
                    .ConfigureAwait(false);
                if (target is null)
                {
                    return (false,
                        $"Warp transitif vers MapId={targetId} : aucune révision publiée (brouillon non exposé).",
                        PlaytestFailureKind.NotPublished,
                        null);
                }

                maps[targetId] = target;
                queue.Enqueue(targetId);
            }
        }

        return (true, null, PlaytestFailureKind.Validation, maps);
    }

    private static HashSet<Guid> CollectDirectWarpTargetIds(Map map)
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
