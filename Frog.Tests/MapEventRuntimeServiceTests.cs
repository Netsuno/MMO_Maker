using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core;
using Frog.Core.Character;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Persistence;
using Frog.Server.Services;
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
        var service = CreateService(catalog, worldState, payload);

        var session = CreateSession(characterId);
        var placement = CreatePlacement(42);

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
        var service = CreateService(catalog, worldState, payload);

        var session = CreateSession(characterId);
        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(7));
        Assert.NotNull(result);
        Assert.False(result!.Success);
    }

    [Fact]
    public async Task ExecuteInteract_SetVariable_PersistsVariable()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Counter",
            EditorAliasId = 99,
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
                            Discriminator = MapEventCommandDiscriminators.AddVariable,
                            ParameterJson = """{"variableId":"score","delta":5}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(catalog, worldState, new InMemoryCharacterPayloadReader());
        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(99));
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(result.VariablesChanged);
        Assert.Equal(5, await worldState.GetVariableAsync(characterId, "score"));
    }

    private static MapEventRuntimeService CreateService(
        IPublishedMapEventCatalog catalog,
        InMemoryCharacterWorldStateRepository worldState,
        InMemoryCharacterPayloadReader payload)
    {
        var phase8 = new Phase8InMemoryPublishedContent();
        var characters = new InMemoryCharacterRepository();
        var items = new Phase7PublishedContent();
        var inventoryRepo = new InMemoryInventoryRepository();
        var inventory = new InventoryGameplayService(
            inventoryRepo,
            new InMemoryInventoryTransferRepository(inventoryRepo, new InMemoryEquipmentRepository(), new InMemoryGroundItemRepository(), items),
            new InMemoryGroundItemRepository(),
            items,
            new InMemoryEquipmentRepository());
        var questRepo = new InMemoryCharacterQuestRepository();
        var quests = new QuestGameplayService(
            phase8,
            questRepo,
            new InMemoryQuestMutationRepository(questRepo, characters, inventory, phase8));
        var dialogSessions = new DialogSessionService(phase8, quests);
        var executor = new MapEventCommandExecutor(
            worldState,
            characters,
            inventory,
            items,
            new DialogGameplayService(phase8, dialogSessions),
            quests,
            phase8,
            phase8,
            new InMemoryCharacterProfessionRepository(),
            payload,
            payload,
            new MovementService(MapTestHelpers.CreateMapService(), new ConnectionManager()),
            NullLogger<MapEventCommandExecutor>.Instance);
        return new MapEventRuntimeService(
            catalog,
            new CharacterMutationCoordinator(),
            executor,
            new MapEventExecutionTracker(),
            NullLogger<MapEventRuntimeService>.Instance);
    }

    private static Session CreateSession(Guid characterId) =>
        new()
        {
            Id = Guid.NewGuid(),
            Username = "hero",
            CharacterId = characterId.ToString("D"),
            CharacterGuid = characterId,
        };

    private static MapEventWireEntry CreatePlacement(int catalogId) =>
        new()
        {
            CatalogId = catalogId,
            PlacementId = 1,
            Slug = "gate",
            DisplayName = "Porte",
            TileX = 0,
            TileY = 0,
            TriggerKind = MapEventTriggerKinds.Interact,
        };

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
