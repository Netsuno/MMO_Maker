using Frog.Application.Playtest;
using Frog.Core.Events;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Origine d’un transfert : tuile warp ou commande <c>teleport</c> d’événement.</summary>
public enum MapTransferSourceKind
{
    Warp = 0,
    EventTeleport = 1,
}

/// <summary>Problème visible dans l’éditeur, sans bloquer l’enregistrement.</summary>
public enum MapTransferIssueKind
{
    MissingMap = 0,
    OutOfBounds = 1,
    BlockedTile = 2,
    NotPublished = 3,
}

/// <summary>Lien warp ou téléportation à vérifier contre le catalogue.</summary>
public sealed record MapTransferLink(
    MapTransferSourceKind SourceKind,
    int SourceX,
    int SourceY,
    string SourceLabel,
    Guid? TargetMapId,
    int? TargetRuntimeMapId,
    int TargetX,
    int TargetY);

/// <summary>Carte connue de l’éditeur (brouillon ouvert ou cible chargée).</summary>
public sealed class MapTransferMapSnapshot
{
    public required Guid MapId { get; init; }

    public required string Name { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required IReadOnlySet<(int X, int Y)> BlockedTiles { get; init; }

    /// <summary>Faux si le playtest ne peut pas charger cette carte (pas de révision publiée).</summary>
    public bool IsPublished { get; init; }
}

/// <summary>Alerte française, ancrée sur la tuile source (marqueur ou warp).</summary>
public sealed record MapTransferIssue(
    MapTransferIssueKind Kind,
    MapTransferSourceKind SourceKind,
    int SourceX,
    int SourceY,
    string Message)
{
    public string KindLabel => Kind switch
    {
        MapTransferIssueKind.MissingMap => "Carte manquante",
        MapTransferIssueKind.OutOfBounds => "Hors limites",
        MapTransferIssueKind.BlockedTile => "Tuile bloquée",
        MapTransferIssueKind.NotPublished => "Non publiée",
        _ => "Transfert",
    };
}

/// <summary>Cartes résolues et identifiants runtime du playtest (carte ouverte = 1).</summary>
public sealed class MapTransferContext
{
    public Guid? OpenMapId { get; init; }

    public required IReadOnlyDictionary<Guid, MapTransferMapSnapshot> MapsById { get; init; }

    public required IReadOnlyDictionary<int, MapTransferMapSnapshot> PlaytestMapsByRuntimeId { get; init; }
}

/// <summary>Carte chargée pour la validation. <see cref="Map"/> null si la cible est absente.</summary>
public readonly record struct MapTransferLoadedMap(Map? Map, bool IsPublished);

/// <summary>Extrait les warps et les commandes <c>teleport</c> (y compris branches et événements communs).</summary>
public static class MapTransferScanner
{
    public static List<MapTransferLink> ScanWarps(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var links = new List<MapTransferLink>();
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.Type != Core.Enums.TileType.Warp)
                {
                    continue;
                }

                links.Add(new MapTransferLink(
                    MapTransferSourceKind.Warp,
                    tile.X,
                    tile.Y,
                    $"Warp ({tile.X}, {tile.Y}), couche « {layer.GetDisplayLabel()} »",
                    tile.WarpTargetMapId == Guid.Empty ? null : tile.WarpTargetMapId,
                    TargetRuntimeMapId: null,
                    tile.WarpTargetX,
                    tile.WarpTargetY));
            }
        }

        return links;
    }

    public static void AppendEventTeleports(
        ICollection<MapTransferLink> links,
        int sourceX,
        int sourceY,
        string eventName,
        IReadOnlyList<MapEventPageDefinition>? pages,
        Func<Guid?, int?, (bool Found, string Name, IReadOnlyList<MapEventPageDefinition> Pages)>? lookupCommonEvent = null)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (pages is null || pages.Count == 0)
        {
            return;
        }

        var name = string.IsNullOrWhiteSpace(eventName) ? "Événement" : eventName.Trim();
        var label = $"Événement « {name} » en ({sourceX}, {sourceY})";
        var visited = new HashSet<(Guid Id, int Alias)>();
        foreach (var page in pages)
        {
            WalkCommands(
                links,
                sourceX,
                sourceY,
                label,
                page.Commands,
                lookupCommonEvent,
                visited,
                branchDepth: 0,
                commonDepth: 0);
        }
    }

    private static void WalkCommands(
        ICollection<MapTransferLink> links,
        int sourceX,
        int sourceY,
        string label,
        IReadOnlyList<MapEventCommandDefinition>? commands,
        Func<Guid?, int?, (bool Found, string Name, IReadOnlyList<MapEventPageDefinition> Pages)>? lookupCommonEvent,
        HashSet<(Guid Id, int Alias)> visited,
        int branchDepth,
        int commonDepth)
    {
        if (commands is null)
        {
            return;
        }

        foreach (var command in commands)
        {
            if (command.Discriminator == MapEventCommandDiscriminators.Teleport)
            {
                if (!MapEventParameterSchemas.TryParseTeleport(
                        command.ParameterJson,
                        out var mapId,
                        out var tileX,
                        out var tileY,
                        out _))
                {
                    continue;
                }

                links.Add(new MapTransferLink(
                    MapTransferSourceKind.EventTeleport,
                    sourceX,
                    sourceY,
                    label,
                    TargetMapId: null,
                    mapId,
                    tileX,
                    tileY));
                continue;
            }

            if (command.Discriminator == MapEventCommandDiscriminators.Branch
                && branchDepth < MapEventRuntimeLimits.MaxBranchDepth
                && MapEventParameterSchemas.TryParseBranch(
                    command.ParameterJson,
                    out _,
                    out var thenCommands,
                    out var elseCommands,
                    out _))
            {
                WalkCommands(links, sourceX, sourceY, label, thenCommands, lookupCommonEvent, visited, branchDepth + 1, commonDepth);
                WalkCommands(links, sourceX, sourceY, label, elseCommands, lookupCommonEvent, visited, branchDepth + 1, commonDepth);
                continue;
            }

            if (command.Discriminator != MapEventCommandDiscriminators.CallCommonEvent
                || commonDepth >= MapEventRuntimeLimits.MaxCommonEventRecursionDepth
                || lookupCommonEvent is null
                || !MapEventParameterSchemas.TryParseCallCommonEvent(
                    command.ParameterJson,
                    out var commonEventId,
                    out var editorAliasId,
                    out _))
            {
                continue;
            }

            var key = (commonEventId, editorAliasId ?? 0);
            if (!visited.Add(key))
            {
                continue;
            }

            var resolved = lookupCommonEvent(commonEventId == Guid.Empty ? null : commonEventId, editorAliasId);
            if (!resolved.Found || resolved.Pages.Count == 0)
            {
                continue;
            }

            var commonName = string.IsNullOrWhiteSpace(resolved.Name) ? "événement commun" : resolved.Name.Trim();
            var nestedLabel = label + $" via « {commonName} »";
            foreach (var page in resolved.Pages)
            {
                WalkCommands(
                    links,
                    sourceX,
                    sourceY,
                    nestedLabel,
                    page.Commands,
                    lookupCommonEvent,
                    visited,
                    branchDepth,
                    commonDepth + 1);
            }
        }
    }
}

/// <summary>
/// Construit le contexte de validation. La carte ouverte est le runtime 1 du playtest
/// (elle est publiée au lancement). Les autres cartes du graphe de warps n’entrent
/// dans ce graphe que si elles ont une révision publiée.
/// </summary>
public static class MapTransferCatalogBuilder
{
    private static readonly Guid UnsavedPrimarySentinel = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    public static MapTransferContext Build(
        Map? openMap,
        Guid? openMapId,
        IReadOnlyList<MapCatalogEntry> catalog,
        Func<Guid, MapTransferLoadedMap> loadMap)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(loadMap);

        var byCatalog = new Dictionary<Guid, MapCatalogEntry>(catalog.Count);
        foreach (var entry in catalog)
        {
            byCatalog[entry.MapId] = entry;
        }

        var maps = new Dictionary<Guid, MapTransferMapSnapshot>();
        MapTransferMapSnapshot? openSnap = null;
        var primaryId = openMapId is Guid id && id != Guid.Empty ? id : Guid.Empty;
        if (openMap is not null)
        {
            var name = string.IsNullOrWhiteSpace(openMap.Name) ? "Carte ouverte" : openMap.Name.Trim();
            openSnap = CreateSnapshot(primaryId, name, openMap, isPublished: true);
            if (primaryId != Guid.Empty)
            {
                maps[primaryId] = openSnap;
            }
        }

        var publishedClosure = new HashSet<Guid>();
        if (primaryId != Guid.Empty)
        {
            publishedClosure.Add(primaryId);
        }

        var queue = new Queue<Guid>();
        var seen = new HashSet<Guid>();
        if (openMap is not null)
        {
            EnqueueWarpTargets(openMap, queue, seen);
        }

        while (queue.Count > 0)
        {
            var targetId = queue.Dequeue();
            if (targetId == Guid.Empty || targetId == primaryId)
            {
                continue;
            }

            if (maps.ContainsKey(targetId))
            {
                continue;
            }

            if (!byCatalog.TryGetValue(targetId, out var entry))
            {
                continue;
            }

            var loaded = loadMap(targetId);
            var model = loaded.Map;
            if (model is null)
            {
                continue;
            }

            var snap = CreateSnapshot(targetId, NameOf(entry, model), model, loaded.IsPublished);
            maps[targetId] = snap;
            if (!loaded.IsPublished)
            {
                continue;
            }

            if (publishedClosure.Add(targetId))
            {
                EnqueueWarpTargets(model, queue, seen);
            }
        }

        var runtime = new Dictionary<int, MapTransferMapSnapshot>();
        if (openSnap is not null || publishedClosure.Count > 0)
        {
            var allocator = new RuntimeMapIdAllocator();
            var primaryKey = primaryId != Guid.Empty ? primaryId : UnsavedPrimarySentinel;
            if (openSnap is not null)
            {
                runtime[allocator.Allocate(primaryKey)] = openSnap;
            }

            foreach (var mapId in publishedClosure.Where(id => id != primaryId && id != Guid.Empty).OrderBy(id => id))
            {
                if (maps.TryGetValue(mapId, out var snap))
                {
                    runtime[allocator.Allocate(mapId)] = snap;
                }
            }
        }

        return new MapTransferContext
        {
            OpenMapId = primaryId == Guid.Empty ? null : primaryId,
            MapsById = maps,
            PlaytestMapsByRuntimeId = runtime,
        };
    }

    private static void EnqueueWarpTargets(Map map, Queue<Guid> queue, HashSet<Guid> seen)
    {
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.WarpTargetMapId != Guid.Empty && seen.Add(tile.WarpTargetMapId))
                {
                    queue.Enqueue(tile.WarpTargetMapId);
                }
            }
        }
    }

    private static string NameOf(MapCatalogEntry entry, Map map)
    {
        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            return entry.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(map.Name) ? "Carte" : map.Name.Trim();
    }

    private static MapTransferMapSnapshot CreateSnapshot(Guid mapId, string name, Map map, bool isPublished)
        => new()
        {
            MapId = mapId,
            Name = name,
            Width = map.Width,
            Height = map.Height,
            BlockedTiles = MapCollision.IndexBlockedTiles(map),
            IsPublished = isPublished,
        };
}

/// <summary>Contrôle éditeur des destinations de warp et de téléportation. N’empêche pas l’enregistrement.</summary>
public static class MapTransferValidator
{
    public static IReadOnlyList<MapTransferIssue> Validate(
        IReadOnlyList<MapTransferLink> links,
        MapTransferContext context)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(context);
        var issues = new List<MapTransferIssue>(links.Count);
        var playtestHint = FormatPlaytestHint(context);
        foreach (var link in links)
        {
            if (!TryResolve(link, context, out var snapshot, out var missing))
            {
                var message = missing;
                if (link.SourceKind == MapTransferSourceKind.EventTeleport && playtestHint.Length > 0)
                {
                    message += playtestHint;
                }

                issues.Add(new MapTransferIssue(
                    MapTransferIssueKind.MissingMap,
                    link.SourceKind,
                    link.SourceX,
                    link.SourceY,
                    message));
                continue;
            }

            if (link.SourceKind == MapTransferSourceKind.Warp
                && !snapshot.IsPublished
                && snapshot.MapId != Guid.Empty
                && snapshot.MapId != context.OpenMapId)
            {
                issues.Add(new MapTransferIssue(
                    MapTransferIssueKind.NotPublished,
                    link.SourceKind,
                    link.SourceX,
                    link.SourceY,
                    $"{link.SourceLabel} → « {snapshot.Name} » : la carte n’est pas publiée. Le playtest ne pourra pas l’atteindre."));
            }

            if (link.TargetX < 0
                || link.TargetY < 0
                || link.TargetX >= snapshot.Width
                || link.TargetY >= snapshot.Height)
            {
                issues.Add(new MapTransferIssue(
                    MapTransferIssueKind.OutOfBounds,
                    link.SourceKind,
                    link.SourceX,
                    link.SourceY,
                    $"{link.SourceLabel} → « {snapshot.Name} » : destination ({link.TargetX}, {link.TargetY}) hors limites ({snapshot.Width}×{snapshot.Height})."));
                continue;
            }

            if (snapshot.BlockedTiles.Contains((link.TargetX, link.TargetY)))
            {
                issues.Add(new MapTransferIssue(
                    MapTransferIssueKind.BlockedTile,
                    link.SourceKind,
                    link.SourceX,
                    link.SourceY,
                    $"{link.SourceLabel} → « {snapshot.Name} » : la tuile ({link.TargetX}, {link.TargetY}) est bloquée."));
            }
        }

        return issues
            .OrderBy(i => i.SourceY)
            .ThenBy(i => i.SourceX)
            .ThenBy(i => i.Kind)
            .ThenBy(i => i.Message, StringComparer.Ordinal)
            .ToList();
    }

    public static string FormatIssueList(IReadOnlyList<MapTransferIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        if (issues.Count == 0)
        {
            return "Aucun problème de transfert.";
        }

        return string.Join(Environment.NewLine, issues.Select(i => "• " + i.Message));
    }

    /// <summary>Message d’invite avant playtest. Null s’il n’y a rien à signaler.</summary>
    public static string? FormatPlaytestGateMessage(IReadOnlyList<MapTransferIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        if (issues.Count == 0)
        {
            return null;
        }

        var shown = Math.Min(issues.Count, 6);
        var lines = new List<string>
        {
            "Des transferts vers une autre carte sont invalides. Le playtest peut échouer.",
            string.Empty,
        };
        for (var i = 0; i < shown; i++)
        {
            lines.Add("• " + issues[i].Message);
        }

        if (issues.Count > shown)
        {
            lines.Add($"• … et {issues.Count - shown} autre(s).");
        }

        lines.Add(string.Empty);
        lines.Add("Continuer le playtest quand même ? L’enregistrement de la carte n’est pas bloqué.");
        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryResolve(
        MapTransferLink link,
        MapTransferContext context,
        out MapTransferMapSnapshot snapshot,
        out string missingMessage)
    {
        snapshot = null!;
        if (link.SourceKind == MapTransferSourceKind.EventTeleport)
        {
            var runtimeId = link.TargetRuntimeMapId ?? 0;
            if (runtimeId <= 0)
            {
                missingMessage = $"{link.SourceLabel} : identifiant de carte invalide.";
                return false;
            }

            if (!context.PlaytestMapsByRuntimeId.TryGetValue(runtimeId, out var byRuntime) || byRuntime is null)
            {
                missingMessage = $"{link.SourceLabel} : carte {runtimeId} introuvable.";
                return false;
            }

            snapshot = byRuntime;
            missingMessage = string.Empty;
            return true;
        }

        if (link.TargetMapId is not Guid mapId || mapId == Guid.Empty)
        {
            missingMessage = $"{link.SourceLabel} : identifiant de carte cible vide.";
            return false;
        }

        if (!context.MapsById.TryGetValue(mapId, out var byId) || byId is null)
        {
            missingMessage = $"{link.SourceLabel} : carte cible {mapId.ToString("N")[..8]} introuvable.";
            return false;
        }

        snapshot = byId;
        missingMessage = string.Empty;
        return true;
    }

    private static string FormatPlaytestHint(MapTransferContext context)
    {
        if (context.PlaytestMapsByRuntimeId.Count == 0)
        {
            return string.Empty;
        }

        var parts = context.PlaytestMapsByRuntimeId
            .OrderBy(pair => pair.Key)
            .Take(4)
            .Select(pair => $"{pair.Key} « {pair.Value.Name} »");
        var ellipsis = context.PlaytestMapsByRuntimeId.Count > 4 ? "…" : string.Empty;
        return " Playtest : " + string.Join(", ", parts) + ellipsis + ".";
    }
}
