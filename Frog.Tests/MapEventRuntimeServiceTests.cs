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
using Frog.Core.Weather;
using Frog.Server.Database;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Frog.Server.Persistence;
using Frog.Server.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ServerPlanner = Frog.Server.Gameplay.MapEventExecutionPlanner;

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
    public async Task ExecuteInteract_ShowChoices_DoesNotRunBranches_AndFollowingSwitchStillApplies()
    {
        var characterId = Guid.NewGuid();
        var choices = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowChoices,
            ParameterJson = MapEventParameterSchemas.SerializeShowChoices(
                ["Oui", "Non"],
                MapEventShowChoices.CancelDisallow,
                [
                    [new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.SetSwitch,
                        ParameterJson = """{"switchId":"inside_choice","value":true}""",
                    }],
                    [],
                ],
                []),
        };
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Choice",
            EditorAliasId = 11,
            Pages =
            [
                new MapEventPageDefinition
                {
                    PageOrder = 0,
                    TriggerKind = Phase8MapEventTriggerKinds.Action,
                    Commands =
                    [
                        choices,
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.PlayBgm,
                            ParameterJson = """{"asset":"Assets/Audio/music-loop.wav","volume":70,"fadeMs":0}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.PlaySe,
                            ParameterJson = """{"asset":"Assets/Audio/ui-click.wav","volume":100,"fadeMs":0}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"after_choice","value":true}""",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(catalog, worldState, new InMemoryCharacterPayloadReader());
        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(11));
        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.NotEqual(true, await worldState.GetSwitchAsync(characterId, "inside_choice"));
        Assert.True(await worldState.GetSwitchAsync(characterId, "after_choice"));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
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
            items,
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
        Assert.Equal(plan.Identity.RequestId, plan.Identity.ActivationId);
        Assert.True(plan.Identity.IsInitialActivation);
        Assert.NotEqual(Guid.Empty, plan.Identity.ActivationId);
        Assert.Empty(session.MapEventPendingRequestIds);
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
    public async Task ExecuteInteract_PublicPath_BeginActivation_IsDistinctFromReplayRestore()
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
        Assert.NotEqual(repo.Plans[0].Identity.LedgerKey, repo.Plans[1].Identity.LedgerKey);
        Assert.NotEqual(repo.Plans[0].Identity.ActivationId, repo.Plans[1].Identity.ActivationId);
        Assert.Equal(repo.Plans[0].Identity.RequestId, repo.Plans[0].Identity.ActivationId);
        Assert.True(repo.Plans[0].Identity.IsInitialActivation);
        Assert.Empty(session.MapEventPendingRequestIds);

        var restored = MapEventExecutionIdentity.Restore(
            characterId,
            placement.PlacementId,
            placement.CatalogId,
            repo.Plans[0].Identity.EffectiveActivationId);
        Assert.Equal(repo.Plans[0].Identity.LedgerKey, restored.LedgerKey);
        var replay = await repo.TryExecutePlanAsync(
            MapEventExecutionPlan.Ok(restored, repo.Plans[0].Effects));
        Assert.Equal(MapEventMutationStatus.IdempotentReplay, replay.Status);
        Assert.Equal(3, repo.Plans.Count);
        Assert.True(second.ProfessionsChanged);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_ClientActivationId_IsReusedForReplay()
    {
        var characterId = Guid.NewGuid();
        var activationId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "ClientId",
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
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);
        var session = CreateSession(characterId);
        var placement = CreatePlacement(12);

        var first = await service.TryExecuteInteractAsync(session, placement, activationId);
        var second = await service.TryExecuteInteractAsync(session, placement, activationId);

        Assert.True(first!.Success);
        Assert.True(second!.Success);
        Assert.Equal(2, repo.Plans.Count);
        Assert.Equal(activationId, repo.Plans[0].Identity.EffectiveActivationId);
        Assert.Equal(activationId, repo.Plans[1].Identity.EffectiveActivationId);
        Assert.Equal(repo.Plans[0].Identity.LedgerKey, repo.Plans[1].Identity.LedgerKey);
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
    public async Task ExecuteInteract_TeleportPage_UsesUnifiedTransactionalPath()
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
                            ParameterJson = """{"mapId":1,"tileX":4,"tileY":1}""",
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
        var session = CreateSession(characterId);
        session.CurrentMapId = 1;
        session.PositionX = 0;
        session.PositionY = 0;

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(13));

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Single(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
        Assert.Contains(repo.Plans[0].Effects, c => c.Discriminator == MapEventCommandDiscriminators.Teleport);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.True(result.TeleportApplied);
        Assert.Equal(4, session.PositionX);
        Assert.Equal(1, session.PositionY);
        Assert.Equal("before-teleport", result.ShowText);
    }

    [Fact]
    public async Task ExecuteInteract_SetWeather_OverridesSessionAndFlagsPush()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Meteo",
            EditorAliasId = 74,
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
                            Discriminator = MapEventCommandDiscriminators.SetWeather,
                            ParameterJson = """{"weatherKind":"rain"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"il pleut"}""",
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
        var session = CreateSession(characterId);

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(74));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.True(result.WeatherChanged);
        Assert.Equal(WeatherKindId.Rain, session.WeatherKindOverride);
        Assert.Equal("il pleut", result.ShowText);
        Assert.Equal(MapEventCommandDiscriminators.SetWeather, repo.Plans[0].Effects[0].Discriminator);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.Equal(
            MapEventEffectCommitKind.SessionSide,
            MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.SetWeather));
    }

    [Fact]
    public async Task ExecuteCommands_UnknownWeatherKind_IsLoggedAndDoesNotStopTheRunner()
    {
        var logger = new WarningListLogger();
        var executor = CreateExecutor(
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            logger: logger);
        var session = CreateSession(Guid.NewGuid());
        var state = new MapEventExecutionState();

        var err = await executor.ExecuteCommandsAsync(
            session,
            session.CharacterGuid!.Value,
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.SetWeather,
                    ParameterJson = """{"weatherKind":"snow"}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowText,
                    ParameterJson = """{"text":"toujours"}""",
                },
            ],
            state,
            CancellationToken.None);

        Assert.Null(err);
        Assert.Null(session.WeatherKindOverride);
        Assert.False(state.WeatherChanged);
        Assert.Equal("toujours", state.ShowText);
        Assert.Contains(logger.Warnings, warning => warning.Contains("kind inconnu", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteInteract_ShowPicture_AppliesSessionAndInteractTrailer()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Image",
            EditorAliasId = 81,
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
                            Discriminator = MapEventCommandDiscriminators.ShowPicture,
                            ParameterJson = """{"pictureId":2,"asset":"Assets/Pictures/placeholder.png","x":16,"y":32,"opacity":200,"blend":"add"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"regarde"}""",
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
        var session = CreateSession(characterId);

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(81));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.True(session.Pictures.TryGetValue(2, out var shown));
        Assert.Equal(16, shown.X);
        Assert.Equal(32, shown.Y);
        Assert.Equal(200, shown.Opacity);
        Assert.Equal(MapEventPicture.BlendAdd, shown.Blend);
        Assert.Equal("regarde", result.ShowText);
        Assert.True(MapEventPictureWire.TryTakeInteractMessage(result.ClientInteractMessage, out var ops, out var rest));
        Assert.Equal("regarde", rest);
        var op = Assert.Single(ops);
        Assert.False(op.Erase);
        Assert.Equal(2, op.PictureId);
        Assert.Equal(MapEventPicture.DefaultAsset, op.Asset);
        Assert.Equal(MapEventCommandDiscriminators.ShowPicture, repo.Plans[0].Effects[0].Discriminator);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
    }

    [Fact]
    public async Task ExecuteCommands_ShowThenErasePicture_ClearsTheSlotInMemory()
    {
        var executor = CreateExecutor(
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader());
        var session = CreateSession(Guid.NewGuid());
        var state = new MapEventExecutionState();

        var err = await executor.ExecuteCommandsAsync(
            session,
            session.CharacterGuid!.Value,
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowPicture,
                    ParameterJson = """{"pictureId":4,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"normal"}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ErasePicture,
                    ParameterJson = """{"pictureId":4}""",
                },
            ],
            state,
            CancellationToken.None);

        Assert.Null(err);
        Assert.False(session.Pictures.ContainsKey(4));
        Assert.Equal(2, state.PictureOps.Count);
        Assert.False(state.PictureOps[0].Erase);
        Assert.True(state.PictureOps[1].Erase);
        Assert.True(MapEventPictureWire.TryTakeInteractMessage(
            MapEventPictureWire.Compose(state.PictureOps, null, null, "Image (image)"),
            out var ops,
            out var rest));
        Assert.Equal(2, ops.Count);
        Assert.Equal("Image (image)", rest);
    }

    [Fact]
    public async Task ExecuteInteract_MoveAndTintPicture_UpdatesTheShownSlot()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Image",
            EditorAliasId = 83,
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
                            Discriminator = MapEventCommandDiscriminators.ShowPicture,
                            ParameterJson = """{"pictureId":2,"asset":"Assets/Pictures/placeholder.png","x":16,"y":32,"opacity":200,"blend":"add"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.MovePicture,
                            ParameterJson = """{"pictureId":2,"x":40,"y":8,"opacity":255,"blend":"normal"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.TintPicture,
                            ParameterJson = """{"pictureId":2,"red":255,"green":10,"blue":20,"opacity":180}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"bouge"}""",
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
        var session = CreateSession(characterId);

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(83));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.True(session.Pictures.TryGetValue(2, out var shown));
        Assert.Equal(MapEventPicture.DefaultAsset, shown.Asset);
        Assert.Equal(40, shown.X);
        Assert.Equal(8, shown.Y);
        Assert.Equal(255, shown.Opacity);
        Assert.Equal(MapEventPicture.BlendNormal, shown.Blend);
        Assert.Equal(255, shown.TintRed);
        Assert.Equal(10, shown.TintGreen);
        Assert.Equal(20, shown.TintBlue);
        Assert.Equal(180, shown.TintOpacity);
        Assert.Equal("bouge", result.ShowText);
        Assert.True(MapEventPictureWire.TryTakeInteractMessage(result.ClientInteractMessage, out var ops, out var rest));
        Assert.Equal("bouge", rest);
        Assert.Equal(3, ops.Count);
        Assert.False(ops[0].IsMove);
        Assert.True(ops[1].IsMove);
        Assert.Equal(40, ops[1].X);
        Assert.True(ops[2].IsTint);
        Assert.Equal(180, ops[2].TintOpacity);
        Assert.Equal(MapEventCommandDiscriminators.MovePicture, repo.Plans[0].Effects[1].Discriminator);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
    }

    [Fact]
    public async Task ExecuteCommands_MoveAndTint_KeepTheAsset_MissingSlotIsIgnored()
    {
        var executor = CreateExecutor(
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader());
        var session = CreateSession(Guid.NewGuid());
        var state = new MapEventExecutionState();

        var err = await executor.ExecuteCommandsAsync(
            session,
            session.CharacterGuid!.Value,
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.MovePicture,
                    ParameterJson = """{"pictureId":4,"x":9,"y":9,"opacity":255,"blend":"normal"}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShowPicture,
                    ParameterJson = """{"pictureId":4,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":128,"blend":"subtract"}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.TintPicture,
                    ParameterJson = """{"pictureId":4,"red":1,"green":2,"blue":3,"opacity":40}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.MovePicture,
                    ParameterJson = """{"pictureId":4,"x":12,"y":-4,"opacity":255,"blend":"add"}""",
                },
            ],
            state,
            CancellationToken.None);

        Assert.Null(err);
        Assert.True(session.Pictures.TryGetValue(4, out var shown));
        Assert.Equal(MapEventPicture.DefaultAsset, shown.Asset);
        Assert.Equal(12, shown.X);
        Assert.Equal(-4, shown.Y);
        Assert.Equal(MapEventPicture.BlendAdd, shown.Blend);
        Assert.Equal(1, shown.TintRed);
        Assert.Equal(40, shown.TintOpacity);
        Assert.Equal(4, state.PictureOps.Count);
        Assert.True(state.PictureOps[0].IsMove);
        Assert.True(state.PictureOps[2].IsTint);
        Assert.True(state.PictureOps[3].IsMove);
    }

    [Fact]
    public async Task ExecuteInteract_FadeAndTint_AppliesSessionAndInteractTrailer()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Nuit",
            EditorAliasId = 82,
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
                            Discriminator = MapEventCommandDiscriminators.FadeOutScreen,
                            ParameterJson = """{"durationMs":1000}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowPicture,
                            ParameterJson = """{"pictureId":2,"asset":"Assets/Pictures/placeholder.png","x":4,"y":8,"opacity":255,"blend":"normal"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.TintScreen,
                            ParameterJson = """{"red":12,"green":24,"blue":48,"opacity":80,"durationMs":500}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"nuit"}""",
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
        var session = CreateSession(characterId);

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(82));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Equal(MapEventScreen.MaxChannel, session.ScreenFade);
        Assert.Equal(new MapEventScreenTone(12, 24, 48, 80), session.ScreenTint);
        Assert.True(session.Pictures.ContainsKey(2));
        Assert.Equal("nuit", result.ShowText);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(result.ClientInteractMessage, out var visuals, out var rest));
        Assert.Equal("nuit", rest);
        Assert.Equal(3, visuals.Count);
        Assert.True(visuals[0].Screen?.IsFadeOut);
        Assert.Equal(1000, visuals[0].Screen?.DurationMs);
        Assert.Equal(2, visuals[1].Picture?.PictureId);
        Assert.True(visuals[2].Screen?.IsTint);
        Assert.Equal(80, visuals[2].Screen?.Opacity);
        Assert.Equal(MapEventCommandDiscriminators.FadeOutScreen, repo.Plans[0].Effects[0].Discriminator);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
    }

    [Fact]
    public async Task ExecuteInteract_ShowAnimation_ResolvesEventAndPlayerTiles()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Étincelle",
            EditorAliasId = 83,
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
                            Discriminator = MapEventCommandDiscriminators.ShowAnimation,
                            ParameterJson = """{"animationId":1,"target":"event","durationMs":600}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowAnimation,
                            ParameterJson = """{"animationId":3,"target":"player","durationMs":400}""",
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
        var session = CreateSession(characterId);
        session.PositionX = 2;
        session.PositionY = 3;
        var placement = CreatePlacement(83);
        placement.TileX = 4;
        placement.TileY = 5;

        var result = await service.TryExecuteInteractAsync(session, placement);

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(result.ClientInteractMessage, out var visuals, out var rest));
        Assert.Equal("Porte (gate)", rest);
        Assert.Equal(2, visuals.Count);
        Assert.Equal(MapEventAnimation.SparkId, visuals[0].Animation?.AnimationId);
        Assert.Equal(MapEventAnimation.TargetEvent, visuals[0].Animation?.Target);
        Assert.Equal(4, visuals[0].Animation?.TileX);
        Assert.Equal(5, visuals[0].Animation?.TileY);
        Assert.Equal(MapEventAnimation.HitId, visuals[1].Animation?.AnimationId);
        Assert.Equal(2, visuals[1].Animation?.TileX);
        Assert.Equal(3, visuals[1].Animation?.TileY);
        Assert.True(ServerPlanner.CanExecuteTransactionally(repo.Plans[0].Effects));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
        Assert.Equal(48, Frog.Core.Constants.TileAssetMetrics.TargetTileSizePixels);
    }

    [Fact]
    public async Task ExecuteCommands_FadeOutThenFadeIn_ClearsTheBlackInMemory()
    {
        var executor = CreateExecutor(
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader());
        var session = CreateSession(Guid.NewGuid());
        var state = new MapEventExecutionState();

        var err = await executor.ExecuteCommandsAsync(
            session,
            session.CharacterGuid!.Value,
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.TintScreen,
                    ParameterJson = """{"red":1,"green":2,"blue":3,"opacity":40,"durationMs":0}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.FadeOutScreen,
                    ParameterJson = """{"durationMs":200}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.FadeInScreen,
                    ParameterJson = """{"durationMs":200}""",
                },
            ],
            state,
            CancellationToken.None);

        Assert.Null(err);
        Assert.Equal(0, session.ScreenFade);
        Assert.Equal(new MapEventScreenTone(1, 2, 3, 40), session.ScreenTint);
        Assert.Equal(3, state.ScreenOps.Count);
        Assert.Equal(
            ["screen", "screen", "screen"],
            state.VisualOrder);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(
            MapEventScreenWire.Compose(state.VisualOps, null, null, "Nuit (nuit)"),
            out var ops,
            out var rest));
        Assert.Equal(3, ops.Count);
        Assert.Equal("Nuit (nuit)", rest);
    }

    [Fact]
    public async Task ExecuteCommands_ShakeAndFlash_LeaveTheSettledTone()
    {
        var executor = CreateExecutor(
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader());
        var session = CreateSession(Guid.NewGuid());
        session.ApplyScreenOp(MapEventScreenOp.ForTint(1, 2, 3, 40, 0));
        session.ApplyScreenOp(MapEventScreenOp.ForFadeOut(0));
        var state = new MapEventExecutionState();

        var err = await executor.ExecuteCommandsAsync(
            session,
            session.CharacterGuid!.Value,
            [
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.ShakeScreen,
                    ParameterJson = """{"power":8,"speed":5,"durationMs":400}""",
                },
                new MapEventCommandDefinition
                {
                    Discriminator = MapEventCommandDiscriminators.FlashScreen,
                    ParameterJson = """{"red":255,"green":255,"blue":255,"opacity":170,"durationMs":200}""",
                },
            ],
            state,
            CancellationToken.None);

        Assert.Null(err);
        Assert.Equal(MapEventScreen.MaxChannel, session.ScreenFade);
        Assert.Equal(new MapEventScreenTone(1, 2, 3, 40), session.ScreenTint);
        Assert.Equal(2, state.ScreenOps.Count);
        Assert.True(state.ScreenOps[0].IsShake);
        Assert.Equal(8, state.ScreenOps[0].Power);
        Assert.Equal(5, state.ScreenOps[0].Speed);
        Assert.True(state.ScreenOps[1].IsFlash);
        Assert.Equal(170, state.ScreenOps[1].Opacity);
        var message = MapEventScreenWire.Compose(state.VisualOps, null, null, "Secousse");
        Assert.StartsWith("shake:8:5:400\n", message, StringComparison.Ordinal);
        Assert.Contains("flash:255:255:255:170:200\n", message, StringComparison.Ordinal);
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
    }

    [Fact]
    public async Task ExecuteInteract_StartDialoguePage_UsesUnifiedTransactionalPath()
    {
        var characterId = Guid.NewGuid();
        var dialogueId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
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
                            ParameterJson = $"{{\"dialogueId\":\"{dialogueId:D}\"}}",
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
            mutationRepository: repo,
            configureContent: content => content.RegisterDialogue(new DialogueDefinition
            {
                Id = dialogueId,
                Name = "Guide",
                Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Hello" }],
            }));

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(14));

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Single(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
        Assert.Contains(repo.Plans[0].Effects, c => c.Discriminator == MapEventCommandDiscriminators.StartDialogue);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.NotNull(result.DialogueState);
        Assert.Equal(dialogueId, result.DialogueState!.DialogueId);
        Assert.Equal("Guide", result.DialogueState.Speaker);
        Assert.Contains("Hello", result.DialogueSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_MixedDialogueTeleport_AppliesIntentsAfterCommit()
    {
        var characterId = Guid.NewGuid();
        var dialogueId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "MixedGate",
            EditorAliasId = 16,
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
                            ParameterJson = """{"switchId":"mixed_ok","value":true}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.StartDialogue,
                            ParameterJson = $"{{\"dialogueId\":\"{dialogueId:D}\"}}",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.Teleport,
                            ParameterJson = """{"mapId":1,"tileX":4,"tileY":1}""",
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
            mutationRepository: repo,
            configureContent: content => content.RegisterDialogue(new DialogueDefinition
            {
                Id = dialogueId,
                Name = "Guide",
                Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Warp" }],
            }));
        var session = CreateSession(characterId);
        session.CurrentMapId = 1;
        session.PositionX = 2;
        session.PositionY = 2;

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(16));

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Single(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
        Assert.True(ServerPlanner.AreEffectsTransactional(repo.Plans[0].Effects));
        Assert.Equal(3, repo.Plans[0].Effects.Count);
        Assert.True(result.SwitchesChanged);
        Assert.Contains(result.SwitchChanges, s => s.SwitchId == "mixed_ok" && s.Value);
        Assert.True(result.TeleportApplied);
        Assert.Equal(4, session.PositionX);
        Assert.Equal(1, session.PositionY);
        Assert.NotNull(result.DialogueState);
        Assert.Equal(dialogueId, result.DialogueState!.DialogueId);
        Assert.Equal(repo.Plans[0].Identity.RequestId, repo.Plans[0].Identity.ActivationId);
        Assert.Empty(session.MapEventPendingRequestIds);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_FailedMutation_DoesNotApplySessionIntents()
    {
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "FailWarp",
            EditorAliasId = 17,
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
                            Discriminator = MapEventCommandDiscriminators.Teleport,
                            ParameterJson = """{"mapId":1,"tileX":4,"tileY":1}""",
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
        var session = CreateSession(characterId);
        session.PositionX = 2;
        session.PositionY = 3;

        var result = await service.TryExecuteInteractAsync(session, CreatePlacement(17));

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Equal("injected-fail", result.Message);
        Assert.False(result.TeleportApplied);
        Assert.Equal(2, session.PositionX);
        Assert.Equal(3, session.PositionY);
        Assert.Single(repo.Plans);
    }

    [Fact]
    public async Task WaitResume_PublicPath_SecondLedgerRequestSameActivation()
    {
        var characterId = Guid.NewGuid();
        var tracker = new MapEventExecutionTracker();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "WaitGate",
            EditorAliasId = 18,
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
                            ParameterJson = """{"milliseconds":80}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"wait_done","value":true}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.Teleport,
                            ParameterJson = """{"mapId":1,"tileX":4,"tileY":1}""",
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
            tracker,
            repo);
        var session = CreateSession(characterId);
        session.CurrentMapId = 1;
        session.PositionX = 0;
        session.PositionY = 0;

        var first = await service.TryExecuteInteractAsync(session, CreatePlacement(18));
        Assert.True(first!.Success);
        Assert.Single(repo.Plans);
        Assert.Equal(0, repo.Plans[0].Identity.WaitOrdinal);
        Assert.False(first.TeleportApplied);
        Assert.Equal(0, session.PositionX);
        var prefixUnit = repo.Plans[0].AsTransactionalUnit();
        Assert.NotNull(prefixUnit.ResumePlan);
        Assert.Equal(1, prefixUnit.ResumePlan!.Identity.WaitOrdinal);

        await Task.Delay(120);
        var resumed = await service.TryResumeWaitingAsync(session);

        Assert.Equal(2, repo.Plans.Count);
        Assert.Equal(0, repo.PageCalls);
        Assert.True(repo.Plans[0].Identity.IsSameActivation(repo.Plans[1].Identity));
        Assert.NotEqual(repo.Plans[0].Identity.LedgerKey, repo.Plans[1].Identity.LedgerKey);
        Assert.Equal(1, repo.Plans[1].Identity.WaitOrdinal);
        Assert.Equal(prefixUnit.ResumePlan.Identity.LedgerKey, repo.Plans[1].Identity.LedgerKey);
        Assert.Equal(
            MapEventExecutionIdentity.DeriveLedgerRequestId(repo.Plans[0].Identity.EffectiveActivationId, 1),
            repo.Plans[1].Identity.RequestId);
        Assert.Contains(
            resumed,
            r => r.SwitchChanges.Any(s => s.SwitchId == "wait_done" && s.Value));
        Assert.Contains(resumed, r => r.TeleportApplied);
        Assert.Equal(4, session.PositionX);
        Assert.Equal(1, session.PositionY);
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

    [Fact]
    public async Task ExecuteInteract_CallCommonEvent_SelectsConditionalPage()
    {
        var characterId = Guid.NewGuid();
        var commonId = Guid.Parse("dddddddd-0003-4000-8000-000000000003");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "PagedCaller",
            EditorAliasId = 69,
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
                Name = "Paged helper",
                Pages =
                [
                    new MapEventPageDefinition
                    {
                        PageOrder = 0,
                        Priority = 0,
                        TriggerKind = Phase8MapEventTriggerKinds.Action,
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.ShowText,
                                ParameterJson = """{"text":"CE page unset."}""",
                            },
                        ],
                    },
                    new MapEventPageDefinition
                    {
                        PageOrder = 1,
                        Priority = 10,
                        TriggerKind = Phase8MapEventTriggerKinds.Action,
                        Conditions =
                        [
                            new MapEventConditionDefinition
                            {
                                Kind = MapEventConditionKinds.CharacterSwitch,
                                ParameterJson = """{"switchId":"ce_page","value":true}""",
                            },
                        ],
                        Commands =
                        [
                            new MapEventCommandDefinition
                            {
                                Discriminator = MapEventCommandDiscriminators.ShowText,
                                ParameterJson = """{"text":"CE page set."}""",
                            },
                        ],
                    },
                ],
            }));

        var session = CreateSession(characterId);
        var locked = await service.TryExecuteInteractAsync(session, CreatePlacement(69));
        Assert.NotNull(locked);
        Assert.True(locked!.Success);
        Assert.Equal("CE page unset.", locked.ShowText);

        await worldState.SetSwitchAsync(characterId, "ce_page", true);
        var unlocked = await service.TryExecuteInteractAsync(session, CreatePlacement(69));
        Assert.NotNull(unlocked);
        Assert.True(unlocked!.Success);
        Assert.Equal("CE page set.", unlocked.ShowText);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_NestedCommonEvent_ExpandsChildSetSwitchViaCore()
    {
        var characterId = Guid.NewGuid();
        var parentId = Guid.Parse("dddddddd-0001-4000-8000-000000000001");
        var childId = Guid.Parse("dddddddd-0002-4000-8000-000000000002");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "NestedCaller",
            EditorAliasId = 65,
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
                            ParameterJson = $"{{\"commonEventId\":\"{parentId:D}\"}}",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository
        {
            Handler = plan => new MapEventMutationResult(
                MapEventMutationStatus.Executed,
                null,
                new MapEventExecutionSnapshot
                {
                    ShowText = "from-parent",
                    SwitchesChanged = true,
                    SwitchChanges =
                    [
                        new WorldSwitchWire { SwitchId = "nested_ce_flag", Value = true },
                    ],
                }),
        };
        var service = CreateService(
            catalog,
            new InMemoryCharacterWorldStateRepository(),
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo,
            configureContent: content =>
            {
                content.RegisterCommonEvent(new CommonEventDefinition
                {
                    Id = parentId,
                    Name = "Parent",
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
                                    ParameterJson = $"{{\"commonEventId\":\"{childId:D}\"}}",
                                },
                                new MapEventCommandDefinition
                                {
                                    Discriminator = MapEventCommandDiscriminators.ShowText,
                                    ParameterJson = """{"text":"from-parent"}""",
                                },
                            ],
                        },
                    ],
                });
                content.RegisterCommonEvent(new CommonEventDefinition
                {
                    Id = childId,
                    Name = "Child",
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
                                    ParameterJson = """{"switchId":"nested_ce_flag","value":true}""",
                                },
                            ],
                        },
                    ],
                });
            });

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(65));

        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.Equal(0, repo.PageCalls);
        Assert.Single(repo.Plans);
        var plan = repo.Plans[0];
        Assert.True(plan.IsSuccess, plan.Error);
        Assert.DoesNotContain(plan.Effects, c => c.Discriminator == MapEventCommandDiscriminators.CallCommonEvent);
        Assert.Equal(MapEventCommandDiscriminators.SetSwitch, plan.Effects[0].Discriminator);
        Assert.Contains("nested_ce_flag", plan.Effects[0].ParameterJson, StringComparison.Ordinal);
        Assert.Equal(MapEventCommandDiscriminators.ShowText, plan.Effects[1].Discriminator);
        Assert.True(result.SwitchesChanged);
        Assert.Contains(result.SwitchChanges, s => s.SwitchId == "nested_ce_flag" && s.Value);
        Assert.Equal("from-parent", result.ShowText);
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_NestedCommonEventCycle_FailsPlan()
    {
        var characterId = Guid.NewGuid();
        var idA = Guid.Parse("dddddddd-00a1-4000-8000-000000000001");
        var idB = Guid.Parse("dddddddd-00a2-4000-8000-000000000002");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "CycleCaller",
            EditorAliasId = 66,
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
                            ParameterJson = $"{{\"commonEventId\":\"{idA:D}\"}}",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository();
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(
            catalog,
            worldState,
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo,
            configureContent: content =>
            {
                content.RegisterCommonEvent(new CommonEventDefinition
                {
                    Id = idA,
                    Name = "Alpha",
                    Pages =
                    [
                        new MapEventPageDefinition
                        {
                            Commands =
                            [
                                new MapEventCommandDefinition
                                {
                                    Discriminator = MapEventCommandDiscriminators.SetSwitch,
                                    ParameterJson = """{"switchId":"cycle_ce_probe","value":true}""",
                                },
                                new MapEventCommandDefinition
                                {
                                    Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                                    ParameterJson = $"{{\"commonEventId\":\"{idB:D}\"}}",
                                },
                            ],
                        },
                    ],
                });
                content.RegisterCommonEvent(new CommonEventDefinition
                {
                    Id = idB,
                    Name = "Beta",
                    Pages =
                    [
                        new MapEventPageDefinition
                        {
                            Commands =
                            [
                                new MapEventCommandDefinition
                                {
                                    Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                                    ParameterJson = $"{{\"commonEventId\":\"{idA:D}\"}}",
                                },
                            ],
                        },
                    ],
                });
            });

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(66));

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("Cycle common-event", result.Message, StringComparison.Ordinal);
        Assert.Empty(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
        Assert.False(result.SwitchesChanged);
        Assert.Empty(result.SwitchChanges);
        Assert.Null(await worldState.GetSwitchAsync(characterId, "cycle_ce_probe"));
    }

    [Fact]
    public async Task ExecuteInteract_PublicPath_MissingCommonEvent_FailsPlanWithoutSetSwitch()
    {
        var characterId = Guid.NewGuid();
        var missingId = Guid.Parse("dddddddd-00b1-4000-8000-000000000099");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "MissingCaller",
            EditorAliasId = 67,
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
                            ParameterJson = """{"switchId":"missing_ce_probe","value":true}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                            ParameterJson = $"{{\"commonEventId\":\"{missingId:D}\"}}",
                        },
                    ],
                },
            ],
        });
        var repo = new RecordingMutationRepository();
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(
            catalog,
            worldState,
            new InMemoryCharacterPayloadReader(),
            mutationRepository: repo);

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(67));

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("introuvable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repo.Plans);
        Assert.Equal(0, repo.PageCalls);
        Assert.False(result.SwitchesChanged);
        Assert.Empty(result.SwitchChanges);
        Assert.Null(await worldState.GetSwitchAsync(characterId, "missing_ce_probe"));
    }

    [Fact]
    public async Task ExecuteInteract_InMemoryPath_MissingCommonEvent_DoesNotApplyPriorSetSwitch()
    {
        var characterId = Guid.NewGuid();
        var missingId = Guid.Parse("dddddddd-00b2-4000-8000-000000000099");
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "MissingCallerInMemory",
            EditorAliasId = 68,
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
                            ParameterJson = """{"switchId":"missing_ce_mem","value":true}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                            ParameterJson = $"{{\"commonEventId\":\"{missingId:D}\"}}",
                        },
                    ],
                },
            ],
        });
        var worldState = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(
            catalog,
            worldState,
            new InMemoryCharacterPayloadReader());

        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(68));

        Assert.NotNull(result);
        Assert.False(result!.Success);
        Assert.Contains("introuvable", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await worldState.GetSwitchAsync(characterId, "missing_ce_mem"));
    }

    [Fact]
    public async Task QuickEventPresets_Interact_AdvancesChestDoorAndInn()
    {
        var characterId = Guid.NewGuid();
        await AssertPresetVisitsAsync(characterId, QuickEventPresetKind.Chest, 81, async (world, result, visit) =>
        {
            if (visit == 1)
            {
                Assert.Equal(QuickEventPresetTexts.ChestOpen, result.ShowText);
                return;
            }

            Assert.Equal(QuickEventPresetTexts.ChestEmpty, result.ShowText);
            var draft = RequirePreset("Coffre", QuickEventPresetKind.Chest);
            Assert.Equal(1, await world.GetVariableAsync(characterId, draft.CounterKey!));
            Assert.True(await world.GetSwitchAsync(characterId, draft.SwitchKey));
        }, visits: 2);

        await AssertPresetVisitsAsync(characterId, QuickEventPresetKind.Door, 82, (world, result, visit) =>
        {
            var draft = RequirePreset("Porte", QuickEventPresetKind.Door);
            if (visit == 1)
            {
                Assert.Equal(QuickEventPresetTexts.DoorOpen, result.ShowText);
            }
            else
            {
                Assert.Equal(QuickEventPresetTexts.DoorAlreadyOpen, result.ShowText);
                Assert.True(world.GetSwitchAsync(characterId, draft.SwitchKey).GetAwaiter().GetResult());
            }

            return Task.CompletedTask;
        }, visits: 2);

        await AssertPresetVisitsAsync(characterId, QuickEventPresetKind.Inn, 83, async (world, result, visit) =>
        {
            var draft = RequirePreset("Auberge", QuickEventPresetKind.Inn);
            var nights = await world.GetVariableAsync(characterId, draft.CounterKey!);
            switch (visit)
            {
                case 1:
                    Assert.Equal(QuickEventPresetTexts.InnWelcome, result.ShowText);
                    Assert.Equal(1, nights);
                    Assert.True(await world.GetSwitchAsync(characterId, draft.SwitchKey));
                    break;
                case 2:
                case 3:
                    Assert.Equal(QuickEventPresetTexts.InnReturn, result.ShowText);
                    Assert.Equal(visit, nights);
                    break;
                default:
                    Assert.Equal(QuickEventPresetTexts.InnRegular, result.ShowText);
                    Assert.Equal(visit, nights);
                    break;
            }
        }, visits: 4);
    }

    private async Task AssertPresetVisitsAsync(
        Guid characterId,
        QuickEventPresetKind kind,
        int alias,
        Func<InMemoryCharacterWorldStateRepository, MapEventExecutionResult, int, Task> assertVisit,
        int visits)
    {
        var draft = RequirePreset(QuickEventPresetDraft.DefaultName(kind), kind);
        var definition = new MapEventDefinition
        {
            Name = draft.DisplayName,
            CatalogSlug = draft.Slug,
            EditorAliasId = alias,
            Pages = draft.Pages,
        };
        var world = new InMemoryCharacterWorldStateRepository();
        var service = CreateService(new FakePublishedMapEventCatalog(definition), world, new InMemoryCharacterPayloadReader());
        var session = CreateSession(characterId);
        for (var visit = 1; visit <= visits; visit++)
        {
            var result = await service.TryExecuteInteractAsync(session, CreatePlacement(alias));
            Assert.NotNull(result);
            Assert.True(result!.Success, result.Message);
            await assertVisit(world, result, visit);
        }
    }

    [Fact]
    public async Task OpenShop_PublishedShop_OpensThroughExistingShopToken()
    {
        var shopId = Phase7ContentSeed.DefaultShopId;
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Marchand",
            EditorAliasId = 77,
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
                            Discriminator = MapEventCommandDiscriminators.OpenShop,
                            ParameterJson = $$"""{"shopId":"{{shopId:D}}","shopName":"Échoppe"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.ShowText,
                            ParameterJson = """{"text":"Bienvenue."}""",
                        },
                    ],
                },
            ],
        });
        var service = CreateService(catalog, new InMemoryCharacterWorldStateRepository(), new InMemoryCharacterPayloadReader());
        var result = await service.TryExecuteInteractAsync(CreateSession(Guid.NewGuid()), CreatePlacement(77));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Equal(shopId, result.OpenShopId);
        Assert.Equal("Bienvenue.", result.ShowText);
        Assert.True(MapEventShopOpen.TryTakeInteractMessage(result.ClientInteractMessage, out var parsed, out var rest));
        Assert.Equal(shopId, parsed);
        Assert.Equal("Bienvenue.", rest);
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(MapEventCommandDiscriminators.OpenShop));
        var published = await catalog.ListPublishedAsync();
        Assert.True(ServerPlanner.CanExecuteTransactionally(published[0].Pages[0].Commands));
        Assert.Equal((ushort)11, Frog.Core.Constants.FrogWireProtocol.Version);
    }

    [Fact]
    public async Task OpenShop_MissingShop_ContinuesAndReportsUnavailable()
    {
        var missing = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var world = new InMemoryCharacterWorldStateRepository();
        var characterId = Guid.NewGuid();
        var catalog = new FakePublishedMapEventCatalog(new MapEventDefinition
        {
            Name = "Comptoir vide",
            EditorAliasId = 78,
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
                            Discriminator = MapEventCommandDiscriminators.OpenShop,
                            ParameterJson = $$"""{"shopId":"{{missing:D}}"}""",
                        },
                        new MapEventCommandDefinition
                        {
                            Discriminator = MapEventCommandDiscriminators.SetSwitch,
                            ParameterJson = """{"switchId":"shop_missing","value":true}""",
                        },
                    ],
                },
            ],
        });
        var service = CreateService(catalog, world, new InMemoryCharacterPayloadReader());
        var result = await service.TryExecuteInteractAsync(CreateSession(characterId), CreatePlacement(78));

        Assert.NotNull(result);
        Assert.True(result!.Success, result.Message);
        Assert.Null(result.OpenShopId);
        Assert.Equal(MapEventShopOpen.UnavailableMessage, result.ShowText);
        Assert.Equal(MapEventShopOpen.UnavailableMessage, result.ClientInteractMessage);
        Assert.Equal(true, await world.GetSwitchAsync(characterId, "shop_missing"));
    }

    private static QuickEventPresetDraft RequirePreset(string name, QuickEventPresetKind kind)
    {
        Assert.True(QuickEventPresetDraft.TryCreate(name, kind, out var draft, out var error), error);
        return draft!;
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
        var executor = CreateExecutor(worldState, payload, phase8);
        return new MapEventRuntimeService(
            catalog,
            phase8,
            new CharacterMutationCoordinator(),
            executor,
            tracker ?? new MapEventExecutionTracker(),
            NullLogger<MapEventRuntimeService>.Instance,
            mutationRepository);
    }

    private static MapEventCommandExecutor CreateExecutor(
        InMemoryCharacterWorldStateRepository worldState,
        InMemoryCharacterPayloadReader payload,
        Phase8InMemoryPublishedContent? phase8 = null,
        ILogger<MapEventCommandExecutor>? logger = null)
    {
        phase8 ??= new Phase8InMemoryPublishedContent();
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
        return new MapEventCommandExecutor(
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
            items,
            logger ?? NullLogger<MapEventCommandExecutor>.Instance);
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
        private readonly Dictionary<(Guid CharacterId, Guid RequestId), MapEventExecutionSnapshot> _ledger = new();

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

            var key = plan.Identity.LedgerKey;
            if (_ledger.TryGetValue(key, out var existing))
            {
                return Task.FromResult(new MapEventMutationResult(
                    MapEventMutationStatus.IdempotentReplay,
                    null,
                    existing));
            }

            var snap = SnapshotFromPlan(plan);
            _ledger[key] = snap;
            return Task.FromResult(new MapEventMutationResult(
                MapEventMutationStatus.Executed,
                null,
                snap));
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

        private static MapEventExecutionSnapshot SnapshotFromPlan(MapEventExecutionPlan plan)
        {
            var unit = plan.AsTransactionalUnit();
            var snap = new MapEventExecutionSnapshot
            {
                ActivationId = plan.Identity.EffectiveActivationId,
                WaitOrdinal = plan.Identity.WaitOrdinal,
            };
            if (!unit.IsSuccess)
            {
                return snap;
            }

            foreach (var cmd in unit.CommitEffects)
            {
                switch (cmd.Discriminator)
                {
                    case MapEventCommandDiscriminators.ShowText:
                        if (MapEventParameterSchemas.TryParseShowText(cmd.ParameterJson, out var text, out _))
                        {
                            snap.ShowText = text;
                        }

                        break;
                    case MapEventCommandDiscriminators.SetSwitch:
                        if (MapEventParameterSchemas.TryParseSetSwitch(
                                cmd.ParameterJson,
                                out var switchId,
                                out var value,
                                out _))
                        {
                            snap.RecordSwitch(switchId, value);
                        }

                        break;
                    case MapEventCommandDiscriminators.GiveItem:
                    case MapEventCommandDiscriminators.TakeItem:
                        snap.InventoryChanged = true;
                        break;
                    case MapEventCommandDiscriminators.GiveGold:
                    case MapEventCommandDiscriminators.TakeGold:
                        snap.GoldChanged = true;
                        break;
                    case MapEventCommandDiscriminators.StartQuest:
                    case MapEventCommandDiscriminators.AdvanceQuest:
                    case MapEventCommandDiscriminators.TurnInQuest:
                        snap.QuestsChanged = true;
                        break;
                    case MapEventCommandDiscriminators.LearnProfession:
                        snap.ProfessionsChanged = true;
                        break;
                    case MapEventCommandDiscriminators.StartDialogue:
                        if (MapEventParameterSchemas.TryParseStartDialogue(
                                cmd.ParameterJson,
                                out var dialogueId,
                                out _))
                        {
                            snap.RecordDialogue(dialogueId);
                        }

                        break;
                    case MapEventCommandDiscriminators.Teleport:
                        if (MapEventParameterSchemas.TryParseTeleport(
                                cmd.ParameterJson,
                                out var mapId,
                                out var tileX,
                                out var tileY,
                                out _))
                        {
                            snap.RecordTeleport(mapId, tileX, tileY);
                        }

                        break;
                    case MapEventCommandDiscriminators.SetWeather:
                        if (MapEventParameterSchemas.TryParseSetWeather(
                                cmd.ParameterJson,
                                out var weatherKind,
                                out _))
                        {
                            snap.RecordWeather(weatherKind);
                        }

                        break;
                    case MapEventCommandDiscriminators.ShowPicture:
                        if (MapEventParameterSchemas.TryParseShowPicture(
                                cmd.ParameterJson,
                                out var shown,
                                out _))
                        {
                            snap.RecordPicture(MapEventPictureOp.ForShow(
                                shown.PictureId,
                                shown.Asset,
                                shown.X,
                                shown.Y,
                                shown.Opacity,
                                shown.Blend));
                        }

                        break;
                    case MapEventCommandDiscriminators.MovePicture:
                        if (MapEventParameterSchemas.TryParseMovePicture(cmd.ParameterJson, out var movePicture, out _))
                        {
                            snap.RecordPicture(movePicture);
                        }

                        break;
                    case MapEventCommandDiscriminators.TintPicture:
                        if (MapEventParameterSchemas.TryParseTintPicture(cmd.ParameterJson, out var tintPicture, out _))
                        {
                            snap.RecordPicture(tintPicture);
                        }

                        break;
                    case MapEventCommandDiscriminators.ErasePicture:
                        if (MapEventParameterSchemas.TryParseErasePicture(cmd.ParameterJson, out var eraseId, out _))
                        {
                            snap.RecordPicture(MapEventPictureOp.ForErase(eraseId));
                        }

                        break;
                    case MapEventCommandDiscriminators.FadeOutScreen:
                        if (MapEventParameterSchemas.TryParseFadeScreen(
                                cmd.ParameterJson,
                                fadeOut: true,
                                out var fadeOut,
                                out _))
                        {
                            snap.RecordScreen(fadeOut);
                        }

                        break;
                    case MapEventCommandDiscriminators.FadeInScreen:
                        if (MapEventParameterSchemas.TryParseFadeScreen(
                                cmd.ParameterJson,
                                fadeOut: false,
                                out var fadeIn,
                                out _))
                        {
                            snap.RecordScreen(fadeIn);
                        }

                        break;
                    case MapEventCommandDiscriminators.TintScreen:
                        if (MapEventParameterSchemas.TryParseTintScreen(cmd.ParameterJson, out var tint, out _))
                        {
                            snap.RecordScreen(tint);
                        }

                        break;
                    case MapEventCommandDiscriminators.ShakeScreen:
                        if (MapEventParameterSchemas.TryParseShakeScreen(cmd.ParameterJson, out var shake, out _))
                        {
                            snap.RecordScreen(shake);
                        }

                        break;
                    case MapEventCommandDiscriminators.FlashScreen:
                        if (MapEventParameterSchemas.TryParseFlashScreen(cmd.ParameterJson, out var flash, out _))
                        {
                            snap.RecordScreen(flash);
                        }

                        break;
                    case MapEventCommandDiscriminators.ShowAnimation:
                        if (MapEventParameterSchemas.TryParseShowAnimation(cmd.ParameterJson, out var animation, out _))
                        {
                            snap.RecordAnimation(animation);
                        }

                        break;
                    case MapEventCommandDiscriminators.Wait:
                        if (MapEventParameterSchemas.TryParseWait(cmd.ParameterJson, out var waitMs, out _))
                        {
                            snap.Waiting = true;
                            snap.WaitUntilUtc = DateTimeOffset.UtcNow.AddMilliseconds(waitMs);
                            snap.PendingCommands = unit.Wait?.RemainingEffects;
                        }

                        break;
                }
            }

            return snap;
        }
    }

    private sealed class WarningListLogger : ILogger<MapEventCommandExecutor>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose()
            {
            }
        }
    }
}
