using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Events;
using Frog.Core;
using Frog.Core.Character;
using Frog.Core.Events;
using Frog.Application.Gameplay;
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

    [Fact]
    public async Task WaitResume_ExecutesDeferredSetSwitchAfterDelay()
    {
        var characterId = Guid.NewGuid();
        var tracker = new MapEventExecutionTracker();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "WaitGate",
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
                            Discriminator = MapEventCommandDiscriminators.Wait,
                            ParameterJson = """{"milliseconds":100}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"wait_done","value":true}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var payload = new InMemoryCharacterPayloadReader();
        var service = CreateService(catalog, worldState, payload, tracker);

        var session = CreateSession(characterId);
        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(99));
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.NotEqual(true, await worldState.GetSwitchAsync(characterId, "wait_done"));

        await Task.Delay(150);
        await service.TryResumeWaitingAsync(session);

        Assert.True(await worldState.GetSwitchAsync(characterId, "wait_done"));
    }

    [Fact]
    public async Task ExecuteInteract_GiveItemOnceKey_GrantsOnlyOnce()
    {
        var characterId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Chest",
            EditorAliasId = 50,
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
                            Discriminator = MapEventCommandDiscriminators.GiveItem,
                            ParameterJson = $$"""{"itemId":"{{itemId:D}}","quantity":2,"onceKey":"chest-a"}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var items = new Phase7PublishedContent();
        items.Publish(new ItemDefinition { Id = itemId, Name = "Gem" });
        var inventoryRepo = new InMemoryInventoryRepository();
        var inventory = new InventoryGameplayService(
            inventoryRepo,
            new InMemoryInventoryTransferRepository(inventoryRepo, new InMemoryEquipmentRepository(), new InMemoryGroundItemRepository(), items),
            new InMemoryGroundItemRepository(),
            items,
            new InMemoryEquipmentRepository());
        var phase8 = new Phase8InMemoryPublishedContent();
        var characters = new InMemoryCharacterRepository();
        var classId = Phase7ContentSeed.DefaultClassId;
        var now = DateTimeOffset.UtcNow;
        await characters.SaveAsync(new CharacterRecord(
            characterId,
            Guid.NewGuid(),
            "Hero",
            classId,
            1,
            0,
            0,
            1,
            0,
            100,
            100,
            50,
            50,
            0,
            0,
            false,
            new CharacterStats(10, 10, 10, 10, 10, 10),
            null,
            null,
            null,
            now,
            now));
        var quests = new QuestGameplayService(
            phase8,
            new InMemoryCharacterQuestRepository(),
            new InMemoryQuestMutationRepository(new InMemoryCharacterQuestRepository(), characters, inventory, phase8));
        var payload = new InMemoryCharacterPayloadReader();
        var executor = new MapEventCommandExecutor(
            worldState,
            characters,
            inventory,
            items,
            new DialogGameplayService(phase8, new DialogSessionService(phase8, quests)),
            quests,
            phase8,
            phase8,
            new InMemoryCharacterProfessionRepository(),
            new ProfessionGameplayService(phase8, new InMemoryCharacterProfessionRepository()),
            payload,
            payload,
            new MovementService(MapTestHelpers.CreateMapService(), new ConnectionManager()),
            NullLogger<MapEventCommandExecutor>.Instance);
        var service = new MapEventRuntimeService(
            catalog,
            new CharacterMutationCoordinator(),
            executor,
            new MapEventExecutionTracker(),
            NullLogger<MapEventRuntimeService>.Instance);
        var session = CreateSession(characterId);

        var first = await service.TryExecuteInteractAsync(session, CreatePlacement(50));
        var second = await service.TryExecuteInteractAsync(session, CreatePlacement(50));
        Assert.NotNull(first);
        Assert.True(first!.Success);
        Assert.NotNull(second);
        Assert.True(second!.Success);

        var snapshot = await inventory.GetInventoryAsync(characterId);
        var total = snapshot.Slots.Where(s => s.ItemId == itemId).Sum(s => s.Quantity);
        Assert.Equal(2, total);
    }

    [Fact]
    public async Task ClearForCharacter_CancelsPendingWait()
    {
        var characterId = Guid.NewGuid();
        var tracker = new MapEventExecutionTracker();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "WaitCancel",
            EditorAliasId = 88,
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
                            Discriminator = MapEventCommandDiscriminators.Wait,
                            ParameterJson = """{"milliseconds":5000}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"never","value":true}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(catalog, worldState, new InMemoryCharacterPayloadReader(), tracker);
        var session = CreateSession(characterId);
        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(88));
        Assert.NotNull(result);
        Assert.True(result!.Success);

        tracker.ClearForCharacter(characterId);
        await service.TryResumeWaitingAsync(session);
        Assert.NotEqual(true, await worldState.GetSwitchAsync(characterId, "never"));
    }

    private static MapEventRuntimeService CreateService(
        IPublishedMapEventCatalog catalog,
        InMemoryCharacterWorldStateRepository worldState,
        InMemoryCharacterPayloadReader payload,
        MapEventExecutionTracker? tracker = null)
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
            new ProfessionGameplayService(phase8, new InMemoryCharacterProfessionRepository()),
            payload,
            payload,
            new MovementService(MapTestHelpers.CreateMapService(), new ConnectionManager()),
            NullLogger<MapEventCommandExecutor>.Instance);
        return new MapEventRuntimeService(
            catalog,
            new CharacterMutationCoordinator(),
            executor,
            tracker ?? new MapEventExecutionTracker(),
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
