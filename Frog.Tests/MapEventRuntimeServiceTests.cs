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
        Assert.Contains(result.SwitchChanges, s => s.SwitchId == "door_open" && s.Value);
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
        var resumed = await service.TryResumeWaitingAsync(session);

        Assert.True(await worldState.GetSwitchAsync(characterId, "wait_done"));
        Assert.Contains(
            resumed,
            r => r.SwitchChanges.Any(s => s.SwitchId == "wait_done" && s.Value));
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
            phase8,
            new CharacterMutationCoordinator(),
            executor,
            new MapEventExecutionTracker(),
            NullLogger<MapEventRuntimeService>.Instance,
            mutationRepository: null);
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

    [Fact]
    public async Task ExecuteStepOnAutorunAndParallel_UsePlacementPassedFromSnapshot()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Pulse",
            EditorAliasId = 3,
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.PlayerContact,
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"step"}""",
                        },
                    ],
                },
                new MapEventPageDefinition
                {
                    PageOrder = 1,
                    TriggerKind = Phase8MapEventTriggerKinds.Autorun,
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"auto"}""",
                        },
                    ],
                },
                new MapEventPageDefinition
                {
                    PageOrder = 2,
                    TriggerKind = Phase8MapEventTriggerKinds.Parallel,
                    Commands =
                    [
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"par"}""",
                        },
                    ],
                },
            ],
        });
        var service = CreateService(catalog, new InMemoryCharacterWorldStateRepository(), new InMemoryCharacterPayloadReader());
        var session = CreateSession(characterId);
        var runtimePlacement = new MapEventWireEntry
        {
            CatalogId = 3,
            PlacementId = 9,
            Slug = "pulse",
            DisplayName = "Pulse",
            TileX = 4,
            TileY = 1,
            TriggerKind = MapEventTriggerKinds.StepOn,
        };

        var step = await service.TryExecuteStepOnAsync(session, runtimePlacement);
        Assert.NotNull(step);
        Assert.True(step!.Success);
        Assert.Equal("step", step.ShowText);

        runtimePlacement.TriggerKind = Phase8MapEventTriggerKinds.Autorun;
        var autorun = await service.TryExecuteAutorunAsync(session, runtimePlacement);
        Assert.NotNull(autorun);
        Assert.True(autorun!.Success);
        Assert.Equal("auto", autorun.ShowText);

        runtimePlacement.TriggerKind = Phase8MapEventTriggerKinds.Parallel;
        var parallel = await service.TryExecuteParallelAsync(session, runtimePlacement);
        Assert.NotNull(parallel);
        Assert.True(parallel!.Success);
        Assert.Equal("par", parallel.ShowText);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_PlansViaCoreThenTryExecutePlanAsync()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "BranchGate",
            EditorAliasId = 11,
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
                            Discriminator = MapEventCommandDiscriminators.Branch,
                            ParameterJson =
                                """{"conditionKind":"character_switch","conditionParameterJson":"{\"switchId\":\"ready\",\"value\":true}","thenCommands":[{"discriminator":"show_text","parameterJson":"{\"text\":\"then-ok\"}"}],"elseCommands":[{"discriminator":"show_text","parameterJson":"{\"text\":\"else-no\"}"}]}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.StartQuest,
                            ParameterJson = """{"questId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        await worldState.SetSwitchAsync(characterId, "ready", true);
        var repo = new RecordingMutationRepository
        {
            Handler = plan => new MapEventMutationResult(
                MapEventMutationStatus.Executed,
                null,
                new MapEventExecutionSnapshot
                {
                    ShowText = "tx-show",
                    QuestsChanged = true,
                    ProfessionsChanged = true,
                    RecipesChanged = true,
                    QuestSummary = "Quête démarrée: Test",
                    SwitchesChanged = true,
                    SwitchChanges =
                    [
                        new WorldSwitchWire { SwitchId = "ready", Value = true },
                    ],
                }),
        };
        var service = CreateService(catalog, worldState, new InMemoryCharacterPayloadReader(), mutationRepository: repo);
        var session = CreateSession(characterId);

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(11));

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(0, repo.PageCalls);
        Assert.Single(repo.Plans);
        var plan = repo.Plans[0];
        Assert.True(plan.IsSuccess, plan.Error);
        Assert.Equal(characterId, plan.Identity.CharacterId);
        Assert.Equal(session.GetOrCreateMapEventRequestId(1), plan.Identity.RequestId);
        Assert.DoesNotContain(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.Branch);
        Assert.DoesNotContain(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.CallCommonEvent);
        Assert.Contains(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.ShowText);
        Assert.Contains(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.StartQuest);
        Assert.Contains("then-ok", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
        Assert.Equal("tx-show", result.ShowText);
        Assert.True(result.QuestsChanged);
        Assert.True(result.ProfessionsChanged);
        Assert.True(result.RecipesChanged);
        Assert.Equal("Quête démarrée: Test", result.QuestSummary);
        Assert.True(result.SwitchesChanged);
        Assert.Contains(result.SwitchChanges, s => s.SwitchId == "ready" && s.Value);
        Assert.False(result.TeleportApplied);
        Assert.Null(result.DialogueState);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_ReplayKeepsRequestId()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Replay",
            EditorAliasId = 12,
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
                            Discriminator = MapEventCommandDiscriminators.LearnProfession,
                            ParameterJson = """{"professionId":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"}""",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository();
        var calls = 0;
        repo.Handler = _ =>
        {
            calls++;
            return new MapEventMutationResult(
                calls == 1 ? MapEventMutationStatus.Executed : MapEventMutationStatus.IdempotentReplay,
                null,
                new MapEventExecutionSnapshot
                {
                    ShowText = "Métier acquis.",
                    ProfessionsChanged = true,
                });
        };
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);
        var session = CreateSession(characterId);
        var placement = CreatePlacement(12);

        var first = await service.TryExecuteInteractAsync(session, placement);
        var second = await service.TryExecuteInteractAsync(session, placement);

        Assert.True(first!.Success);
        Assert.True(second!.Success);
        Assert.Equal(2, repo.Plans.Count);
        Assert.Equal(0, repo.PageCalls);
        Assert.Equal(repo.Plans[0].Identity.RequestId, repo.Plans[1].Identity.RequestId);
        Assert.Equal(repo.Plans[0].Identity.LedgerKey, repo.Plans[1].Identity.LedgerKey);
        Assert.True(second.ProfessionsChanged);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_FailedMutation_IsNotPresentedAsSuccess()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "FailTx",
            EditorAliasId = 15,
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
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"should_not_leak","value":true}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.GiveItem,
                            ParameterJson = """{"itemId":"dddddddd-dddd-dddd-dddd-dddddddddddd","quantity":1}""",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository
        {
            Handler = _ => new MapEventMutationResult(MapEventMutationStatus.Failed, "injected-fail"),
        };
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(15));

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("injected-fail", result.Message);
        Assert.False(result.SwitchesChanged);
        Assert.False(result.InventoryChanged);
        Assert.Single(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
    }

    [Fact]
    public async Task ExecuteInteract_TeleportPage_DoesNotCallMutationRepository()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Warp",
            EditorAliasId = 13,
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
                            ParameterJson = """{"text":"before-teleport"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.Teleport,
                            ParameterJson = """{"mapId":1,"tileX":0,"tileY":0}""",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository();
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);

        await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(13));

        Assert.Empty(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
    }

    [Fact]
    public async Task ExecuteInteract_StartDialoguePage_DoesNotCallMutationRepository()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Talk",
            EditorAliasId = 14,
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
                            Discriminator = MapEventCommandDiscriminators.StartDialogue,
                            ParameterJson = """{"dialogueId":"cccccccc-cccc-cccc-cccc-cccccccccccc"}""",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository();
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);

        await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(14));

        Assert.Empty(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
    }

    [Fact]
    public async Task ExecuteInteract_CallCommonEvent_SetSwitch_RecordsSwitchChange()
    {
        var characterId = Guid.NewGuid();
        var commonId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Caller",
            EditorAliasId = 64,
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
                            Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                            ParameterJson = $"{{\"commonEventId\":\"{commonId}\"}}",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(
            catalog,
            worldState,
            new InMemoryCharacterPayloadReader(),
            configureContent: content => content.RegisterCommonEvent(new CommonEventDefinition
            {
                Id = commonId,
                Name = "Helper",
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
                                Discriminator = MapEventCommandDiscriminators.SetSwitch,
                                ParameterJson = """{"switchId":"phase8_common_fired","value":true}""",
                            },
                        ],
                    },
                ],
            }));

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(64));
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.True(result.SwitchesChanged);
        Assert.Contains(result.SwitchChanges, s => s.SwitchId == "phase8_common_fired" && s.Value);
        Assert.True(await worldState.GetSwitchAsync(characterId, "phase8_common_fired"));
    }

    private static MapEventRuntimeService CreateService(
        IPublishedMapEventCatalog catalog,
        InMemoryCharacterWorldStateRepository worldState,
        InMemoryCharacterPayloadReader payload,
        MapEventExecutionTracker? tracker = null,
        IMapEventMutationRepository? mutationRepository = null,
        Action<Phase8InMemoryPublishedContent>? configureContent = null)
    {
        var phase8 = new Phase8InMemoryPublishedContent();
        configureContent?.Invoke(phase8);
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
            phase8,
            new CharacterMutationCoordinator(),
            executor,
            tracker ?? new MapEventExecutionTracker(),
            NullLogger<MapEventRuntimeService>.Instance,
            mutationRepository);
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

    private sealed class RecordingMutationRepository : IMapEventMutationRepository
    {
        public List<MapEventExecutionPlan> Plans { get; } = [];

        public int PageCalls { get; private set; }

        public Func<MapEventExecutionPlan, MapEventMutationResult>? Handler { get; set; }

        public Task<MapEventMutationResult> TryExecutePlanAsync(
            MapEventExecutionPlan plan,
            CancellationToken cancellationToken = default)
        {
            Plans.Add(plan);
            if (Handler is not null)
            {
                return Task.FromResult(Handler(plan));
            }

            return Task.FromResult(new MapEventMutationResult(
                MapEventMutationStatus.Executed,
                null,
                new MapEventExecutionSnapshot()));
        }

        public Task<MapEventMutationResult> TryExecutePageAsync(
            Guid characterId,
            Guid requestId,
            long placementId,
            int catalogAliasId,
            IReadOnlyList<MapEventCommandDefinition> commands,
            CancellationToken cancellationToken = default)
        {
            PageCalls++;
            return TryExecutePlanAsync(
                MapEventExecutionPlan.Ok(
                    new MapEventExecutionIdentity(requestId, characterId, placementId, catalogAliasId),
                    commands),
                cancellationToken);
        }
    }
}
