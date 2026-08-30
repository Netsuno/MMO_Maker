using Frog.Application.Content;
using Frog.Application.Gameplay;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Gameplay;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Frog.Persistence.PostgreSql.Repositories.Player;
using Frog.Server.Gameplay;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests.Support;

public sealed record Phase8PostgresContentSeedResult(
    Phase7PostgresContentSeedResult Phase7,
    Guid DialogueId,
    long DialoguePublishedRevision,
    Guid QuestId,
    Guid DraftQuestId,
    Guid ProfessionId,
    Guid RecipeId,
    Guid RegionId,
    Guid WeatherProfileId,
    Guid GateMapEventId,
    int GateMapEventAliasId,
    Guid KeyMapEventId,
    int KeyMapEventAliasId,
    Guid AutorunMapEventId,
    Guid ContactMapEventId,
    int ContactMapEventAliasId,
    Guid ParallelMapEventId,
    int ParallelMapEventAliasId,
    Guid RouteMapEventId,
    int RouteMapEventAliasId,
    Guid WaitMapEventId,
    int WaitMapEventAliasId,
    Guid LearnProfessionMapEventId,
    int LearnProfessionMapEventAliasId,
    Guid MapId,
    int RuntimeMapId,
    int GateEventTileX,
    int GateEventTileY,
    int KeyEventTileX,
    int KeyEventTileY,
    int ContactEventTileX,
    int ContactEventTileY,
    int ParallelEventTileX,
    int ParallelEventTileY,
    int RouteEventTileX,
    int RouteEventTileY,
    int RouteBlockTileX,
    int RouteBlockTileY,
    int WaitEventTileX,
    int WaitEventTileY,
    int LearnProfessionTileX,
    int LearnProfessionTileY,
    int QuestRewardGold,
    string GateSwitchId,
    string WaitSwitchId,
    byte ExpectedLightingLevel);

/// <summary>Publie le contenu Phase 8 minimal (Guids déterministes) dans PostgreSQL.</summary>
public static class Phase8PostgresContentSeed
{
    public static readonly Guid DefaultDialogueId = Guid.Parse("bbbbbbbb-0001-4000-8000-000000000001");
    public static readonly Guid DefaultQuestId = Guid.Parse("bbbbbbbb-0002-4000-8000-000000000001");
    public static readonly Guid DraftQuestId = Guid.Parse("bbbbbbbb-0099-4000-8000-000000000001");
    public static readonly Guid DefaultProfessionId = Guid.Parse("bbbbbbbb-0003-4000-8000-000000000001");
    public static readonly Guid DefaultRecipeId = Guid.Parse("bbbbbbbb-0004-4000-8000-000000000001");
    public static readonly Guid DefaultRegionId = Guid.Parse("bbbbbbbb-0005-4000-8000-000000000001");
    public static readonly Guid DefaultWeatherProfileId = Guid.Parse("bbbbbbbb-0006-4000-8000-000000000001");

    public const int GateMapEventAliasId = 8101;
    public const int KeyMapEventAliasId = 8102;
    public const int ContactMapEventAliasId = 8104;
    public const int ParallelMapEventAliasId = 8105;
    public const int RouteMapEventAliasId = 8106;
    public const int WaitMapEventAliasId = 8107;
    public const int LearnProfessionMapEventAliasId = 8108;
    public const string GateSwitchId = "phase8_gate";
    public const string WaitSwitchId = "phase8_wait_done";
    public const int QuestRewardGold = 50;

    public const int GateEventTileX = 1;
    public const int GateEventTileY = 0;
    public const int KeyEventTileX = 0;
    public const int KeyEventTileY = 1;
    public const int ContactEventTileX = 2;
    public const int ContactEventTileY = 0;
    public const int ParallelEventTileX = 0;
    public const int ParallelEventTileY = 2;
    public const int RouteEventTileX = 4;
    public const int RouteEventTileY = 0;
    public const int RouteBlockTileX = 4;
    public const int RouteBlockTileY = 1;
    /// <summary>Dwell on the block tile so E2E collision checks stay deterministic (route loops otherwise).</summary>
    public const int RouteBlockDwellMs = 30_000;
    public const int WaitEventTileX = 5;
    public const int WaitEventTileY = 1;
    public const int LearnProfessionTileX = 0;
    public const int LearnProfessionTileY = 3;

    public static async Task<Phase8PostgresContentSeedResult> PublishAsync(FrogDbContextGate gate)
    {
        var phase7 = await Phase7PostgresContentSeed.PublishAsync(gate, monsterSpawnCount: 0).ConfigureAwait(false);

        var phase8Repo = new PostgresPhase8PublishedCatalogs(gate);
        var dialogueRevision = await EnsurePublishedDialogueAsync(phase8Repo, DefaultQuestId).ConfigureAwait(false);
        await EnsurePublishedQuestAsync(phase8Repo, phase7.ConsumableId).ConfigureAwait(false);
        await EnsureDraftQuestAsync(phase8Repo).ConfigureAwait(false);
        await EnsurePublishedProfessionAsync(phase8Repo).ConfigureAwait(false);
        await EnsurePublishedRecipeAsync(phase8Repo, phase7.ConsumableId).ConfigureAwait(false);
        await EnsurePublishedWeatherAsync(phase8Repo).ConfigureAwait(false);

        var runtimeMapId = phase7.RuntimeMapId;
        await EnsurePublishedRegionAsync(phase8Repo, runtimeMapId).ConfigureAwait(false);

        var mapEvents = new PostgresMapEventRepository(gate);
        var (gateEventId, keyEventId, autorunEventId, contactEventId, parallelEventId, routeEventId, waitEventId, learnProfessionEventId) =
            await EnsurePublishedMapEventsAsync(mapEvents).ConfigureAwait(false);

        await EnsureMapEventPlacementsAsync(
            gate,
            phase7.MapId,
            gateEventId,
            keyEventId,
            autorunEventId,
            contactEventId,
            parallelEventId,
            routeEventId,
            waitEventId,
            learnProfessionEventId).ConfigureAwait(false);

        var lighting = (byte)Math.Clamp((int)(CreateDefaultWeather().LightingFactor * 255), 0, 255);

        return new Phase8PostgresContentSeedResult(
            phase7,
            DefaultDialogueId,
            dialogueRevision,
            DefaultQuestId,
            DraftQuestId,
            DefaultProfessionId,
            DefaultRecipeId,
            DefaultRegionId,
            DefaultWeatherProfileId,
            gateEventId,
            GateMapEventAliasId,
            keyEventId,
            KeyMapEventAliasId,
            autorunEventId,
            contactEventId,
            ContactMapEventAliasId,
            parallelEventId,
            ParallelMapEventAliasId,
            routeEventId,
            RouteMapEventAliasId,
            waitEventId,
            WaitMapEventAliasId,
            learnProfessionEventId,
            LearnProfessionMapEventAliasId,
            phase7.MapId,
            runtimeMapId,
            GateEventTileX,
            GateEventTileY,
            KeyEventTileX,
            KeyEventTileY,
            ContactEventTileX,
            ContactEventTileY,
            ParallelEventTileX,
            ParallelEventTileY,
            RouteEventTileX,
            RouteEventTileY,
            RouteBlockTileX,
            RouteBlockTileY,
            WaitEventTileX,
            WaitEventTileY,
            LearnProfessionTileX,
            LearnProfessionTileY,
            QuestRewardGold,
            GateSwitchId,
            WaitSwitchId,
            lighting);
    }

    public static DialogueDefinition CreateDefaultDialogue(Guid questId) => new()
    {
        Id = DefaultDialogueId,
        Name = "Phase8Guide",
        EditorAliasId = 8001,
        Lines =
        [
            new DialogueLineDefinition { Speaker = "Guide", Text = "Will you help?" },
        ],
        Choices =
        [
            new DialogueChoiceDefinition
            {
                ChoiceId = "accept",
                Label = "Accept",
                StartQuestId = questId,
            },
        ],
    };

    public static QuestDefinition CreateDefaultQuest(Guid consumableId) => new()
    {
        Id = DefaultQuestId,
        Name = "Phase8 E2E Quest",
        EditorAliasId = 8002,
        Stages =
        [
            new QuestStageDefinition
            {
                Description = "Craft the potion bundle",
                Objectives =
                [
                    new QuestObjectiveDefinition
                    {
                        Kind = QuestObjectiveKind.Craft,
                        Description = "Craft potion bundle",
                        RequiredCount = 1,
                        TargetRecipeId = DefaultRecipeId,
                    },
                ],
            },
        ],
        CompletionReward = new QuestRewardDefinition
        {
            Gold = QuestRewardGold,
            ItemId = consumableId,
            ItemQuantity = 1,
        },
    };

    public static QuestDefinition CreateDraftQuest() => new()
    {
        Id = DraftQuestId,
        Name = "Draft Only Quest",
        Stages = [new QuestStageDefinition { Description = "Never published" }],
    };

    public static ProfessionDefinition CreateDefaultProfession() => new()
    {
        Id = DefaultProfessionId,
        Name = "Alchemist",
        MaxLevel = 100,
    };

    public static RecipeDefinition CreateDefaultRecipe(Guid consumableId) => new()
    {
        Id = DefaultRecipeId,
        Name = "Potion Bundle",
        ProfessionId = DefaultProfessionId,
        RequiredProfessionLevel = 1,
        OutputItemId = consumableId,
        OutputQuantity = 1,
        Ingredients =
        [
            new RecipeIngredientDefinition { ItemId = consumableId, Quantity = 2 },
        ],
    };

    public static WeatherProfileDefinition CreateDefaultWeather() => new()
    {
        Id = DefaultWeatherProfileId,
        Name = "Overcast",
        WeatherKind = "rain",
        LightingFactor = 0.5f,
    };

    public static RegionDefinition CreateDefaultRegion(int runtimeMapId) => new()
    {
        Id = DefaultRegionId,
        Name = "Spawn Region",
        MapId = runtimeMapId,
        TileXMin = 0,
        TileYMin = 0,
        TileXMax = 5,
        TileYMax = 5,
        WeatherProfileId = DefaultWeatherProfileId,
    };

    public static async Task RepublishDialogueAsync(FrogDbContextGate gate, string newText)
    {
        var repo = new PostgresPhase8PublishedCatalogs(gate);
        var stored = await repo.LoadDraftByIdAsync(DefaultDialogueId).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Dialogue draft missing.");
        var dialogue = CreateDefaultDialogue(DefaultQuestId);
        dialogue.Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = newText }];
        var payload = Phase8ContentCodec.SerializeDialogue(dialogue);
        var saved = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            ContentId = DefaultDialogueId,
            Kind = Phase8ContentKind.Dialogue,
            Name = dialogue.Name,
            PayloadJson = payload,
            ExpectedRevision = stored.Revision,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
        if (saved is not Phase8SaveContentResult.Success)
        {
            throw new InvalidOperationException("Dialogue republish failed: " + saved.GetType().Name);
        }
    }

    public static async Task SeedProfessionProgressAsync(FrogDbContextGate gate, Guid characterId, int level = 1)
    {
        var repo = new PostgresCharacterProfessionRepository(gate);
        await repo.UpsertAsync(new CharacterProfessionProgress
        {
            CharacterId = characterId,
            ProfessionId = DefaultProfessionId,
            Level = level,
            Experience = 0,
        }).ConfigureAwait(false);
    }

    public static async Task SeedInventoryIngredientsAsync(
        FrogDbContextGate gate,
        Guid characterId,
        Guid consumableId,
        int quantity = 2)
    {
        var items = new PostgresItemRepository(gate);
        var itemDef = await items.LoadPublishedByIdAsync(consumableId).ConfigureAwait(false)
                      ?? throw new InvalidOperationException("Consumable missing.");
        var inventory = new PostgresInventoryRepository(gate);
        var added = await inventory.TryAddAsync(characterId, consumableId, quantity, itemDef.Definition.MaxStack)
            .ConfigureAwait(false);
        if (added.Status != InventoryMutationStatus.Ok)
        {
            throw new InvalidOperationException("Ingredient seed failed: " + added.ErrorMessage);
        }
    }

    private static async Task<long> EnsurePublishedDialogueAsync(
        PostgresPhase8PublishedCatalogs repo,
        Guid questId)
    {
        IPublishedDialogueCatalog catalog = repo;
        var existing = await catalog.TryGetPublishedByIdAsync(DefaultDialogueId).ConfigureAwait(false);
        if (existing is not null)
        {
            var startQuest = existing.Choices.FirstOrDefault()?.StartQuestId;
            if (startQuest == questId)
            {
                var stored = await repo.LoadDraftByIdAsync(DefaultDialogueId).ConfigureAwait(false);
                return stored?.PublishedRevision ?? 1;
            }

            var draft = await repo.LoadDraftByIdAsync(DefaultDialogueId).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Dialogue draft missing for repair.");
            var repaired = CreateDefaultDialogue(questId);
            var repairPayload = Phase8ContentCodec.SerializeDialogue(repaired);
            var repairedSave = await repo.SaveAsync(new Phase8SaveContentRequest
            {
                ContentId = DefaultDialogueId,
                Kind = Phase8ContentKind.Dialogue,
                Name = repaired.Name,
                EditorAliasId = repaired.EditorAliasId,
                PayloadJson = repairPayload,
                ExpectedRevision = draft.Revision,
                Intent = SaveContentIntent.Publish,
            }).ConfigureAwait(false);
            return AssertSuccess(repairedSave).PublishedRevision ?? 1;
        }

        var dialogue = CreateDefaultDialogue(questId);
        var payload = Phase8ContentCodec.SerializeDialogue(dialogue);
        var saved = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultDialogueId,
            Kind = Phase8ContentKind.Dialogue,
            Name = dialogue.Name,
            EditorAliasId = dialogue.EditorAliasId,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
        return AssertSuccess(saved).PublishedRevision ?? 1;
    }

    private static async Task EnsurePublishedQuestAsync(PostgresPhase8PublishedCatalogs repo, Guid consumableId)
    {
        IPublishedQuestCatalog catalog = repo;
        if (await catalog.TryGetPublishedByIdAsync(DefaultQuestId).ConfigureAwait(false) is not null)
        {
            return;
        }

        var quest = CreateDefaultQuest(consumableId);
        var payload = Phase8ContentCodec.SerializeQuest(quest);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultQuestId,
            Kind = Phase8ContentKind.Quest,
            Name = quest.Name,
            EditorAliasId = quest.EditorAliasId,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
    }

    private static async Task EnsureDraftQuestAsync(PostgresPhase8PublishedCatalogs repo)
    {
        IPublishedQuestCatalog catalog = repo;
        if (await repo.LoadDraftByIdAsync(DraftQuestId).ConfigureAwait(false) is not null)
        {
            return;
        }

        if (await catalog.TryGetPublishedByIdAsync(DraftQuestId).ConfigureAwait(false) is not null)
        {
            return;
        }

        var quest = CreateDraftQuest();
        var payload = Phase8ContentCodec.SerializeQuest(quest);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DraftQuestId,
            Kind = Phase8ContentKind.Quest,
            Name = quest.Name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }).ConfigureAwait(false);
    }

    private static async Task EnsurePublishedProfessionAsync(PostgresPhase8PublishedCatalogs repo)
    {
        IPublishedProfessionCatalog catalog = repo;
        if (await catalog.TryGetPublishedByIdAsync(DefaultProfessionId).ConfigureAwait(false) is not null)
        {
            return;
        }

        var profession = CreateDefaultProfession();
        var payload = Phase8ContentCodec.SerializeProfession(profession);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultProfessionId,
            Kind = Phase8ContentKind.Profession,
            Name = profession.Name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
    }

    private static async Task EnsurePublishedRecipeAsync(PostgresPhase8PublishedCatalogs repo, Guid consumableId)
    {
        IPublishedRecipeCatalog catalog = repo;
        if (await catalog.TryGetPublishedByIdAsync(DefaultRecipeId).ConfigureAwait(false) is not null)
        {
            return;
        }

        var recipe = CreateDefaultRecipe(consumableId);
        var payload = Phase8ContentCodec.SerializeRecipe(recipe);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultRecipeId,
            Kind = Phase8ContentKind.Recipe,
            Name = recipe.Name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
    }

    private static async Task EnsurePublishedWeatherAsync(PostgresPhase8PublishedCatalogs repo)
    {
        IPublishedWeatherCatalog catalog = repo;
        if (await catalog.TryGetPublishedByIdAsync(DefaultWeatherProfileId).ConfigureAwait(false) is not null)
        {
            return;
        }

        var weather = CreateDefaultWeather();
        var payload = Phase8ContentCodec.SerializeWeather(weather);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultWeatherProfileId,
            Kind = Phase8ContentKind.WeatherProfile,
            Name = weather.Name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
    }

    private static async Task EnsurePublishedRegionAsync(PostgresPhase8PublishedCatalogs repo, int runtimeMapId)
    {
        IPublishedRegionCatalog catalog = repo;
        var existing = await catalog.TryGetRegionForTileAsync(runtimeMapId, 0, 0).ConfigureAwait(false);
        if (existing is not null && existing.Id == DefaultRegionId)
        {
            return;
        }

        var region = CreateDefaultRegion(runtimeMapId);
        var payload = Phase8ContentCodec.SerializeRegion(region);
        _ = await repo.SaveAsync(new Phase8SaveContentRequest
        {
            NewId = DefaultRegionId,
            Kind = Phase8ContentKind.Region,
            Name = region.Name,
            PayloadJson = payload,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
    }

    private static async Task<(Guid GateId, Guid KeyId, Guid AutorunId, Guid ContactId, Guid ParallelId, Guid RouteId, Guid WaitId, Guid LearnProfessionId)> EnsurePublishedMapEventsAsync(
        PostgresMapEventRepository mapEvents)
    {
        var gateId = await EnsureMapEventAsync(
            mapEvents,
            GateMapEventAliasId,
            "Phase8 Gate",
            CreateGateMapEventDefinition()).ConfigureAwait(false);
        var keyId = await EnsureMapEventAsync(
            mapEvents,
            KeyMapEventAliasId,
            "Phase8 Key",
            CreateKeyMapEventDefinition()).ConfigureAwait(false);
        var autorunId = await EnsureMapEventAsync(
            mapEvents,
            8103,
            "Phase8 Autorun",
            CreateAutorunMapEventDefinition()).ConfigureAwait(false);
        var contactId = await EnsureMapEventAsync(
            mapEvents,
            ContactMapEventAliasId,
            "Phase8 Contact",
            CreateContactMapEventDefinition()).ConfigureAwait(false);
        var parallelId = await EnsureMapEventAsync(
            mapEvents,
            ParallelMapEventAliasId,
            "Phase8 Parallel",
            CreateParallelMapEventDefinition()).ConfigureAwait(false);
        var routeId = await EnsureMapEventAsync(
            mapEvents,
            RouteMapEventAliasId,
            "Phase8 Route Blocker",
            CreateRouteMapEventDefinition()).ConfigureAwait(false);
        var waitId = await EnsureMapEventAsync(
            mapEvents,
            WaitMapEventAliasId,
            "Phase8 Wait",
            CreateWaitMapEventDefinition()).ConfigureAwait(false);
        var learnProfessionId = await EnsureMapEventAsync(
            mapEvents,
            LearnProfessionMapEventAliasId,
            "Phase8 Learn Profession",
            CreateLearnProfessionMapEventDefinition()).ConfigureAwait(false);
        return (gateId, keyId, autorunId, contactId, parallelId, routeId, waitId, learnProfessionId);
    }

    private static async Task<Guid> EnsureMapEventAsync(
        PostgresMapEventRepository mapEvents,
        int aliasId,
        string name,
        MapEventDefinition definition)
    {
        definition.EditorAliasId = aliasId;
        definition.Name = name;
        var existing = await mapEvents.TryGetPublishedByAliasAsync(aliasId).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        var saved = await mapEvents.SaveAsync(new SaveMapEventRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }).ConfigureAwait(false);
        return AssertSaveSuccess(saved).EventId;
    }

    private static MapEventDefinition CreateGateMapEventDefinition() => new()
    {
        Name = "Phase8 Gate",
        EditorAliasId = GateMapEventAliasId,
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
                        ParameterJson = """{"text":"Gate locked."}""",
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
                        ParameterJson = $$"""{"switchId":"{{GateSwitchId}}","value":true}""",
                    },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.StartDialogue,
                        ParameterJson = $$"""{"dialogueId":"{{DefaultDialogueId:D}}"}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateKeyMapEventDefinition() => new()
    {
        Name = "Phase8 Key",
        EditorAliasId = KeyMapEventAliasId,
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
                        ParameterJson = $$"""{"switchId":"{{GateSwitchId}}","value":true}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Key turned."}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateAutorunMapEventDefinition() => new()
    {
        Name = "Phase8 Autorun",
        EditorAliasId = 8103,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Autorun,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Welcome to Phase8."}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateContactMapEventDefinition() => new()
    {
        Name = "Phase8 Contact",
        EditorAliasId = ContactMapEventAliasId,
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
                        Discriminator = MapEventCommandDiscriminators.GiveItem,
                        ParameterJson = $$"""{"itemId":"{{Phase7ContentSeed.DefaultItemId:D}}","quantity":2}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateParallelMapEventDefinition() => new()
    {
        Name = "Phase8 Parallel",
        EditorAliasId = ParallelMapEventAliasId,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Parallel,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Parallel pulse."}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateRouteMapEventDefinition() => new()
    {
        Name = "Phase8 Route Blocker",
        EditorAliasId = RouteMapEventAliasId,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                MovementKind = MapEventMovementKinds.Route,
                BlocksCollision = true,
                RouteWaypoints =
                [
                    new MapEventRouteWaypoint { TileX = RouteEventTileX, TileY = RouteEventTileY, WaitMs = 250 },
                    new MapEventRouteWaypoint { TileX = RouteBlockTileX, TileY = RouteBlockTileY, WaitMs = RouteBlockDwellMs },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Route patrol."}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateWaitMapEventDefinition() => new()
    {
        Name = "Phase8 Wait",
        EditorAliasId = WaitMapEventAliasId,
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
                        ParameterJson = """{"milliseconds":300}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.SetSwitch,
                        ParameterJson = $$"""{"switchId":"{{WaitSwitchId}}","value":true}""",
                    },
                ],
            },
        ],
    };

    private static MapEventDefinition CreateLearnProfessionMapEventDefinition() => new()
    {
        Name = "Phase8 Learn Profession",
        EditorAliasId = LearnProfessionMapEventAliasId,
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
                        ParameterJson = $$"""{"professionId":"{{DefaultProfessionId:D}}"}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Profession learned."}""",
                    },
                ],
            },
        ],
    };

    private static async Task EnsureMapEventPlacementsAsync(
        FrogDbContextGate gate,
        Guid mapId,
        Guid gateEventId,
        Guid keyEventId,
        Guid autorunEventId,
        Guid contactEventId,
        Guid parallelEventId,
        Guid routeEventId,
        Guid waitEventId,
        Guid learnProfessionEventId)
    {
        await gate.ExecuteAsync(async (db, ct) =>
        {
            var existing = await db.MapEventPlacements.AsNoTracking()
                .Where(p => p.MapId == mapId)
                .Select(p => p.EventDefinitionId)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            if (existing.Contains(learnProfessionEventId))
            {
                return;
            }

            var routeWaypoints = MapEventPersistenceMapper.SerializeRouteWaypoints(
            [
                new MapEventRouteWaypoint { TileX = RouteEventTileX, TileY = RouteEventTileY, WaitMs = 250 },
                new MapEventRouteWaypoint { TileX = RouteBlockTileX, TileY = RouteBlockTileY, WaitMs = RouteBlockDwellMs },
            ]);

            db.MapEventPlacements.AddRange(
                CreatePlacement(mapId, gateEventId, GateEventTileX, GateEventTileY, Phase8MapEventTriggerKinds.Action),
                CreatePlacement(mapId, keyEventId, KeyEventTileX, KeyEventTileY, Phase8MapEventTriggerKinds.Action),
                CreatePlacement(mapId, autorunEventId, 3, 3, Phase8MapEventTriggerKinds.Autorun),
                CreatePlacement(mapId, contactEventId, ContactEventTileX, ContactEventTileY, Phase8MapEventTriggerKinds.PlayerContact),
                CreatePlacement(mapId, parallelEventId, ParallelEventTileX, ParallelEventTileY, Phase8MapEventTriggerKinds.Parallel),
                CreateRoutePlacement(mapId, routeEventId, RouteEventTileX, RouteEventTileY, routeWaypoints),
                CreatePlacement(mapId, waitEventId, WaitEventTileX, WaitEventTileY, Phase8MapEventTriggerKinds.Action),
                CreatePlacement(mapId, learnProfessionEventId, LearnProfessionTileX, LearnProfessionTileY, Phase8MapEventTriggerKinds.Action));
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        var maps = new PostgresMapRepository(gate);
        var stored = await maps.LoadByIdAsync(mapId).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Map missing for republish.");
        var republish = await maps.SaveAsync(new SaveMapRequest
        {
            MapId = mapId,
            Map = stored.Map,
            ExpectedRevision = stored.Revision,
            Intent = SaveMapIntent.Publish,
        }).ConfigureAwait(false);
        _ = AssertMapSaveSuccess(republish);
    }

    private static MapEventPlacementEntity CreateRoutePlacement(
        Guid mapId,
        Guid eventId,
        int tileX,
        int tileY,
        string routeWaypointsJson) =>
        new()
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            EventDefinitionId = eventId,
            TileX = tileX,
            TileY = tileY,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            MovementKind = MapEventMovementKinds.Route,
            RouteWaypointsJson = routeWaypointsJson,
        };

    private static MapEventPlacementEntity CreatePlacement(
        Guid mapId,
        Guid eventId,
        int tileX,
        int tileY,
        string triggerKind) =>
        new()
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            EventDefinitionId = eventId,
            TileX = tileX,
            TileY = tileY,
            TriggerKind = triggerKind,
            MovementKind = MapEventMovementKinds.Fixed,
            RouteWaypointsJson = "[]",
        };

    private static Phase8SaveContentResult.Success AssertSuccess(Phase8SaveContentResult result) =>
        result as Phase8SaveContentResult.Success
        ?? throw new InvalidOperationException("Phase8 content save failed: " + result.GetType().Name);

    private static SaveMapEventResult.Success AssertSaveSuccess(SaveMapEventResult result) =>
        result as SaveMapEventResult.Success
        ?? throw new InvalidOperationException("Map event save failed: " + result.GetType().Name);

    private static SaveMapResult.Success AssertMapSaveSuccess(SaveMapResult result) =>
        result as SaveMapResult.Success
        ?? throw new InvalidOperationException("Map save failed: " + result.GetType().Name);
}
