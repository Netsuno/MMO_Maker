using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Persistence.IntegrationTests.Support;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Server.Database;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresMapEventRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresMapEventRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task SavePublishAndLoadPublished_EventDefinition_RoundTrips()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var repo = new PostgresMapEventRepository(gate);

        var definition = new MapEventDefinition
        {
            Name = "Test Gate",
            CatalogSlug = "test-gate",
            EditorAliasId = 9001,
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                    Conditions =
                    [
                        new MapEventConditionDefinition
                        {
                            Kind = MapEventConditionKinds.CharacterSwitch,
                            ParameterJson = """{"switchId":"door_open","value":true}""",
                        },
                    ],
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"Welcome"}""",
                        },
                    ],
                },
            ],
        };

        var save = await repo.SaveAsync(new SaveMapEventRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        var success = Assert.IsType<SaveMapEventResult.Success>(save);

        var published = await repo.LoadPublishedByIdAsync(success.EventId);
        Assert.NotNull(published);
        Assert.Equal("Test Gate", published!.Definition.Name);
        Assert.Single(published.Definition.Pages);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, published.Definition.Pages[0].Commands[0].Discriminator);
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task PublishedPlacements_ResolveForRuntimeMap()
    {
        using var gate = new FrogDbContextGate(new FrogDbContext(FrogDbContextOptions.Create(_fixture.ConnectionString)));
        var mapRepo = new PostgresMapRepository(gate);
        var eventRepo = new PostgresMapEventRepository(gate);

        var eventDef = new MapEventDefinition
        {
            Name = "Placed",
            EditorAliasId = 9002,
            Pages = [new MapEventPageDefinition { PageOrder = 0 }],
        };
        var eventSave = await eventRepo.SaveAsync(new SaveMapEventRequest
        {
            Definition = eventDef,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        var eventId = Assert.IsType<SaveMapEventResult.Success>(eventSave).EventId;

        var map = new Frog.Core.Models.Map
        {
            Name = "EventMap",
            Width = 8,
            Height = 8,
        };
        var mapSave = await mapRepo.SaveAsync(new Frog.Application.Maps.SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = Frog.Application.Maps.SaveMapIntent.SaveDraft,
        });
        var mapId = Assert.IsType<Frog.Application.Maps.SaveMapResult.Success>(mapSave).MapId;

        await gate.ExecuteAsync(async (db, ct) =>
        {
            db.MapEventPlacements.Add(new MapEventPlacementEntity
            {
                Id = Guid.NewGuid(),
                MapId = mapId,
                EventDefinitionId = eventId,
                TileX = 2,
                TileY = 3,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Fixed,
                RouteWaypointsJson = "[]",
            });
            await db.SaveChangesAsync(ct);
        });

        var publish = await mapRepo.SaveAsync(new Frog.Application.Maps.SaveMapRequest
        {
            MapId = mapId,
            Map = map,
            ExpectedRevision = 1,
            Intent = Frog.Application.Maps.SaveMapIntent.Publish,
        });
        Assert.IsType<Frog.Application.Maps.SaveMapResult.Success>(publish);

        int runtimeMapId = 0;
        await gate.ExecuteAsync(async (db, ct) =>
        {
            runtimeMapId = await db.RuntimeMapBindings.AsNoTracking()
                .Where(b => b.MapId == mapId)
                .Select(b => b.RuntimeMapId)
                .SingleAsync(ct);
        });

        var placements = await eventRepo.GetPlacementsForRuntimeMapAsync(runtimeMapId);
        Assert.Single(placements);
        Assert.Equal(2, placements[0].TileX);
        Assert.Equal(3, placements[0].TileY);
        Assert.Equal(9002, placements[0].CatalogId);
    }

    private static Map CreateValidMap(string name, int width, int height)
    {
        var map = new Map { Name = name, Width = width, Height = height };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    Type = TileType.Ground,
                });
            }
        }

        map.Layers.Add(ground);
        return map;
    }
}
