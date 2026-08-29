using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventRuntimeServiceTests
{
    [Fact]
    public async Task ExecuteInteract_ShowTextAndSetSwitch_PersistsSwitchAndReturnsText()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Gate",
            EditorAliasId = 42,
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"Porte ouverte"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"door_open","value":true}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var payload = new InMemoryCharacterPayloadReader();
        var service = new MapEventRuntimeService(
            catalog,
            worldState,
            new CharacterMutationCoordinator(),
            payload,
            payload,
            NullLogger<MapEventRuntimeService>.Instance);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Username = "hero",
            CharacterId = characterId.ToString("D"),
            CharacterGuid = characterId,
        };
        var placement = new MapEventWireEntry
        {
            CatalogId = 42,
            PlacementId = 1,
            Slug = "gate",
            DisplayName = "Porte",
            TileX = 0,
            TileY = 0,
            TriggerKind = MapEventTriggerKinds.Interact,
        };

        var result = await service.TryExecuteInteractAsync(session, placement);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal("Porte ouverte", result.ShowText);
        Assert.True(result.SwitchesChanged);
        Assert.True(await worldState.GetSwitchAsync(characterId, "door_open"));
    }

    [Fact]
    public async Task ExecuteInteract_SkipsPageWhenSwitchConditionFails()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Gate",
            EditorAliasId = 7,
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
                            ParameterJson = """{"text":"Fermé"}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var payload = new InMemoryCharacterPayloadReader();
        var service = new MapEventRuntimeService(
            catalog,
            worldState,
            new CharacterMutationCoordinator(),
            payload,
            payload,
            NullLogger<MapEventRuntimeService>.Instance);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Username = "hero",
            CharacterId = characterId.ToString("D"),
            CharacterGuid = characterId,
        };

        var result = await service.TryExecuteInteractAsync(
            session,
            new MapEventWireEntry
            {
                CatalogId = 7,
                PlacementId = 1,
                Slug = "gate",
                DisplayName = "Porte",
                TileX = 0,
                TileY = 0,
                TriggerKind = MapEventTriggerKinds.Interact,
            });
        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    private sealed class FakePublishedMapEventCatalog(MapEventDefinition definition) : IPublishedMapEventCatalog
    {
        public Task<IReadOnlyList<MapEventDefinition>> ListPublishedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MapEventDefinition>>([definition]);

        public Task<MapEventDefinition?> TryGetPublishedByIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
            Task.FromResult<MapEventDefinition?>(definition.Id == eventId ? definition : null);

        public Task<MapEventDefinition?> TryGetPublishedByAliasAsync(
            int editorAliasId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MapEventDefinition?>(
                definition.EditorAliasId == editorAliasId ? definition : null);
    }
}
