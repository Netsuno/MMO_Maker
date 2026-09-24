using System;
using System.Collections.Generic;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapTransferValidatorTests
{
    [Fact]
    public void Warp_ToUnknownMap_IsMissingMap()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var missingId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var open = MapOf("Prairie", 8, 6);
        open.Layers[0].Tiles.Add(Warp(2, 3, missingId, 1, 1));

        var issues = Validate(open, openId, catalog: new[] { Entry(openId, "Prairie", 8, 6, published: true) }, maps: new Dictionary<Guid, Map>());

        var issue = Assert.Single(issues);
        Assert.Equal(MapTransferIssueKind.MissingMap, issue.Kind);
        Assert.Equal(2, issue.SourceX);
        Assert.Equal(3, issue.SourceY);
        Assert.Contains("introuvable", issue.Message, StringComparison.Ordinal);
        Assert.Contains("bbbbbbbb", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warp_DestinationOutOfBounds_IsReported()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var targetId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3");
        var open = MapOf("Prairie", 8, 6);
        open.Layers[0].Tiles.Add(Warp(1, 1, targetId, 9, 0));
        var target = MapOf("Donjon", 4, 4);

        var issues = Validate(
            open,
            openId,
            catalog: new[]
            {
                Entry(openId, "Prairie", 8, 6, published: true),
                Entry(targetId, "Donjon", 4, 4, published: true),
            },
            maps: new Dictionary<Guid, Map> { [targetId] = target });

        var issue = Assert.Single(issues);
        Assert.Equal(MapTransferIssueKind.OutOfBounds, issue.Kind);
        Assert.Contains("hors limites", issue.Message, StringComparison.Ordinal);
        Assert.Contains("4×4", issue.Message, StringComparison.Ordinal);
        Assert.Contains("(9, 0)", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Warp_OntoBlockedTile_IsReported_WithoutBlockingAValidWarp()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var targetId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3");
        var open = MapOf("Prairie", 8, 6);
        open.Layers[0].Tiles.Add(Warp(0, 0, targetId, 1, 1));
        open.Layers[0].Tiles.Add(Warp(4, 2, targetId, 2, 2));
        var target = MapOf("Donjon", 5, 5);
        target.Layers[0].Tiles.Add(new Tile { X = 1, Y = 1, Type = TileType.Block });

        var issues = Validate(
            open,
            openId,
            catalog: new[]
            {
                Entry(openId, "Prairie", 8, 6, published: true),
                Entry(targetId, "Donjon", 5, 5, published: true),
            },
            maps: new Dictionary<Guid, Map> { [targetId] = target });

        var issue = Assert.Single(issues);
        Assert.Equal(MapTransferIssueKind.BlockedTile, issue.Kind);
        Assert.Equal(0, issue.SourceX);
        Assert.Equal(0, issue.SourceY);
        Assert.Contains("bloquée", issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EventTeleport_UnknownRuntimeMap_IsMissing_AndOutOfBoundsOnOpenMap()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var open = MapOf("Prairie", 4, 4);
        var links = MapTransferScanner.ScanWarps(open);
        MapTransferScanner.AppendEventTeleports(
            links,
            1,
            2,
            "Garde",
            new[]
            {
                Page(
                    Teleport(99, 0, 0),
                    Teleport(1, 20, 1)),
            });

        var context = MapTransferCatalogBuilder.Build(
            open,
            openId,
            new[] { Entry(openId, "Prairie", 4, 4, published: true) },
            _ => new MapTransferLoadedMap(null, false));
        var issues = MapTransferValidator.Validate(links, context);

        Assert.Contains(issues, i => i.Kind == MapTransferIssueKind.MissingMap && i.Message.Contains("carte 99", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Kind == MapTransferIssueKind.OutOfBounds && i.SourceKind == MapTransferSourceKind.EventTeleport);
        Assert.Contains(issues, i => i.Message.Contains("Playtest : 1 « Prairie »", StringComparison.Ordinal));
        Assert.All(issues, i => Assert.Equal((1, 2), (i.SourceX, i.SourceY)));
    }

    [Fact]
    public void BranchAndCommonEvent_TeleportOutOfBounds_IsReported()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var open = MapOf("Prairie", 3, 3);
        var links = new List<MapTransferLink>();
        MapTransferScanner.AppendEventTeleports(
            links,
            0,
            1,
            "Porte",
            new[]
            {
                Page(new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.Branch,
                    ParameterJson =
                        """
                        {"conditionKind":"character_switch","conditionParameterJson":"{\"switchId\":\"ready\",\"value\":true}","thenCommands":[{"discriminator":"teleport","parameterJson":"{\"mapId\":1,\"tileX\":8,\"tileY\":0}"}],"elseCommands":[{"discriminator":"call_common_event","parameterJson":"{\"commonEventId\":\"dddddddd-dddd-dddd-dddd-ddddddddddd4\"}"}]}
                        """,
                }),
            },
            (_, _) => (true, "Ouverture", new[] { Page(Teleport(1, 0, 9)) }));

        var context = MapTransferCatalogBuilder.Build(
            open,
            openId,
            new[] { Entry(openId, "Prairie", 3, 3, published: false) },
            _ => new MapTransferLoadedMap(null, false));
        var issues = MapTransferValidator.Validate(links, context);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(MapTransferIssueKind.OutOfBounds, i.Kind));
        Assert.Contains(issues, i => i.Message.Contains("via « Ouverture »", StringComparison.Ordinal));
        Assert.Contains(issues, i => i.Message.Contains("(8, 0)", StringComparison.Ordinal));
    }

    [Fact]
    public void UnpublishedWarpTarget_IsWarned_AndPlaytestGateStaysOptional()
    {
        var openId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var targetId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3");
        var open = MapOf("Prairie", 6, 6);
        open.Layers[0].Tiles.Add(Warp(2, 2, targetId, 0, 0));
        var target = MapOf("Cave", 4, 4);

        var issues = Validate(
            open,
            openId,
            catalog: new[]
            {
                Entry(openId, "Prairie", 6, 6, published: false),
                Entry(targetId, "Cave", 4, 4, published: false),
            },
            maps: new Dictionary<Guid, Map> { [targetId] = target },
            publishedIds: new HashSet<Guid>());

        var issue = Assert.Single(issues);
        Assert.Equal(MapTransferIssueKind.NotPublished, issue.Kind);
        Assert.Contains("n’est pas publiée", issue.Message, StringComparison.Ordinal);

        var gate = MapTransferValidator.FormatPlaytestGateMessage(issues);
        Assert.NotNull(gate);
        Assert.Contains("Continuer le playtest quand même", gate, StringComparison.Ordinal);
        Assert.Contains("n’est pas bloqué", gate, StringComparison.Ordinal);
        Assert.Null(MapTransferValidator.FormatPlaytestGateMessage(Array.Empty<MapTransferIssue>()));
    }

    private static IReadOnlyList<MapTransferIssue> Validate(
        Map open,
        Guid openId,
        IReadOnlyList<MapCatalogEntry> catalog,
        IReadOnlyDictionary<Guid, Map> maps,
        IReadOnlySet<Guid>? publishedIds = null)
    {
        var context = MapTransferCatalogBuilder.Build(
            open,
            openId,
            catalog,
            mapId =>
            {
                if (!maps.TryGetValue(mapId, out var map) || map is null)
                {
                    return new MapTransferLoadedMap(null, false);
                }

                return new MapTransferLoadedMap(map, publishedIds?.Contains(mapId) ?? true);
            });
        return MapTransferValidator.Validate(MapTransferScanner.ScanWarps(open), context);
    }

    private static MapCatalogEntry Entry(Guid id, string name, int width, int height, bool published)
        => new()
        {
            MapId = id,
            Name = name,
            Width = width,
            Height = height,
            Revision = 1,
            Status = published ? MapPublishStatus.Published : MapPublishStatus.Draft,
            PublishedRevision = published ? 1 : null,
        };

    private static Map MapOf(string name, int width, int height)
    {
        var map = new Map { Name = name, Width = width, Height = height };
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributs" });
        return map;
    }

    private static Tile Warp(int x, int y, Guid target, int tx, int ty)
        => new()
        {
            X = x,
            Y = y,
            Type = TileType.Warp,
            WarpTargetMapId = target,
            WarpTargetX = tx,
            WarpTargetY = ty,
        };

    private static MapEventPageDefinition Page(params MapEventCommandDefinition[] commands)
        => new()
        {
            PageOrder = 0,
            TriggerKind = "action",
            Commands = commands,
        };

    private static MapEventCommandDefinition Teleport(int mapId, int x, int y)
        => new()
        {
            Discriminator = MapEventCommandDiscriminators.Teleport,
            ParameterJson = $$"""{"mapId":{{mapId}},"tileX":{{x}},"tileY":{{y}}}""",
        };
}
