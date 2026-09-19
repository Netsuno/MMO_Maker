using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Application.Demo;

/// <summary>
/// Monde de démonstration P10-4 (fixture de validation, pas le contenu final FRoG).
/// Identifiants Guid déterministes. Cartes 20×20, tuiles procédurales (TilesetId 1).
/// </summary>
public static class Phase10DemoWorldCatalog
{
    public const string VillageMapName = "Village d'accueil";
    public const string OutskirtsMapName = "Faubourgs";
    public const string ArenaMapName = "Arène";

    public static readonly Guid ClassId = Guid.Parse("cccccccc-0001-4000-8000-000000000001");
    public static readonly Guid SpellId = Guid.Parse("cccccccc-0002-4000-8000-000000000001");

    public static readonly Guid PotionId = Guid.Parse("cccccccc-0003-4000-8000-000000000001");
    public static readonly Guid BandageId = Guid.Parse("cccccccc-0003-4000-8000-000000000002");
    public static readonly Guid HerbId = Guid.Parse("cccccccc-0003-4000-8000-000000000003");
    public static readonly Guid ClothId = Guid.Parse("cccccccc-0003-4000-8000-000000000004");
    public static readonly Guid SwordId = Guid.Parse("cccccccc-0003-4000-8000-000000000005");
    public static readonly Guid TunicId = Guid.Parse("cccccccc-0003-4000-8000-000000000006");
    public static readonly Guid BreadId = Guid.Parse("cccccccc-0003-4000-8000-000000000007");
    public static readonly Guid ArenaKeyId = Guid.Parse("cccccccc-0003-4000-8000-000000000008");

    public static readonly Guid SlimeId = Guid.Parse("cccccccc-0004-4000-8000-000000000001");
    public static readonly Guid WolfId = Guid.Parse("cccccccc-0004-4000-8000-000000000002");

    public static readonly Guid GuideNpcId = Guid.Parse("cccccccc-0005-4000-8000-000000000001");
    public static readonly Guid MerchantNpcId = Guid.Parse("cccccccc-0005-4000-8000-000000000002");
    public static readonly Guid CrafterNpcId = Guid.Parse("cccccccc-0005-4000-8000-000000000003");

    public static readonly Guid ShopId = Guid.Parse("cccccccc-0006-4000-8000-000000000001");
    public static readonly Guid ProfessionId = Guid.Parse("cccccccc-0007-4000-8000-000000000001");
    public static readonly Guid BandageRecipeId = Guid.Parse("cccccccc-0008-4000-8000-000000000001");
    public static readonly Guid BreadRecipeId = Guid.Parse("cccccccc-0008-4000-8000-000000000002");
    public static readonly Guid DialogueId = Guid.Parse("cccccccc-0009-4000-8000-000000000001");
    public static readonly Guid FirstStepsQuestId = Guid.Parse("cccccccc-000a-4000-8000-000000000001");
    public static readonly Guid TrialQuestId = Guid.Parse("cccccccc-000a-4000-8000-000000000002");
    public static readonly Guid VillageWeatherId = Guid.Parse("cccccccc-000b-4000-8000-000000000001");
    public static readonly Guid WildsWeatherId = Guid.Parse("cccccccc-000b-4000-8000-000000000002");
    public static readonly Guid VillageRegionId = Guid.Parse("cccccccc-000c-4000-8000-000000000001");
    public static readonly Guid WildsRegionId = Guid.Parse("cccccccc-000c-4000-8000-000000000002");
    public static readonly Guid WelcomeCommonEventId = Guid.Parse("cccccccc-000d-4000-8000-000000000001");
    public static readonly Guid OnceRewardEventId = Guid.Parse("cccccccc-000e-4000-8000-000000000001");
    public static readonly Guid LearnProfessionEventId = Guid.Parse("cccccccc-000e-4000-8000-000000000002");
    public static readonly Guid DialogueEventId = Guid.Parse("cccccccc-000e-4000-8000-000000000003");
    public static readonly Guid CommonCallerEventId = Guid.Parse("cccccccc-000e-4000-8000-000000000004");

    public const string OnceRewardKey = "p10_demo_chest";
    public const string WelcomeSwitchId = "p10_demo_welcome";

    public const int VillageWarpOutX = 18;
    public const int VillageWarpOutY = 10;
    public const int OutskirtsWarpVillageX = 1;
    public const int OutskirtsWarpVillageY = 10;
    public const int OutskirtsWarpArenaX = 18;
    public const int OutskirtsWarpArenaY = 10;
    public const int ArenaWarpInX = 1;
    public const int ArenaWarpInY = 10;
    public const int SpawnTileX = 2;
    public const int SpawnTileY = 2;
    public const int VisitTileX = 10;
    public const int VisitTileY = 10;
    public const int CollectTileX = 8;
    public const int CollectTileY = 8;
    public const int GuideTileX = 4;
    public const int GuideTileY = 4;
    public const int MerchantTileX = 6;
    public const int MerchantTileY = 4;
    public const int CrafterTileX = 8;
    public const int CrafterTileY = 4;
    public const int SlimeTileX = 12;
    public const int SlimeTileY = 12;
    public const int WolfTileX = 10;
    public const int WolfTileY = 10;
    public const int ChestTileX = 3;
    public const int ChestTileY = 6;
    public const int WelcomeEventTileX = 2;
    public const int WelcomeEventTileY = 3;

    public static IReadOnlyList<ItemDefinition> CreateItems() =>
    [
        Item(PotionId, "Potion de village", ItemType.Consumable, "icons/items/potion.png", 20, 25, 10, "Soin de base."),
        Item(BandageId, "Bandage", ItemType.Consumable, "icons/items/bandage.png", 20, 15, 6, "Fabriqué par l'herboriste."),
        Item(HerbId, "Herbe des faubourgs", ItemType.Quest, "icons/items/herb.png", 20, 5, 2, "Ingrédient de craft."),
        Item(ClothId, "Tissu", ItemType.Quest, "icons/items/cloth.png", 20, 8, 3, "Ingrédient de bandage."),
        Item(SwordId, "Épée de garde", ItemType.Weapon, "icons/items/sword.png", 1, 120, 40, "Arme de départ."),
        Item(TunicId, "Tunique de village", ItemType.Armor, "icons/items/tunic.png", 1, 80, 30, "Armure légère."),
        Item(BreadId, "Pain de seigle", ItemType.Consumable, "icons/items/bread.png", 20, 12, 4, "Fabriqué au four."),
        Item(ArenaKeyId, "Clé de l'arène", ItemType.Key, "icons/items/key.png", 1, 0, 0, "Récompense unique."),
    ];

    public static IReadOnlyList<NpcDefinition> CreateFriendlyNpcs() =>
    [
        Npc(GuideNpcId, "Guide", NpcKind.Npc, "sprites/npcs/guide.png", 1, "Accueil et quêtes."),
        Npc(MerchantNpcId, "Marchand", NpcKind.Npc, "sprites/npcs/merchant.png", 1, "Boutique du village."),
        Npc(CrafterNpcId, "Artisan", NpcKind.Npc, "sprites/npcs/crafter.png", 1, "Métier herboriste."),
    ];

    public static IReadOnlyList<NpcDefinition> CreateMonsters() =>
    [
        Npc(SlimeId, "Slime des prés", NpcKind.Monster, "sprites/npcs/slime.png", 1, "Monstre des faubourgs."),
        Npc(WolfId, "Loup de l'arène", NpcKind.Monster, "sprites/npcs/wolf.png", 3, "Monstre de la zone finale."),
    ];

    public static SpellDefinition CreateSpell() => new()
    {
        Id = SpellId,
        Name = "Éclat",
        Kind = SpellKind.Spell,
        ManaCost = 6,
        CooldownMs = 1000,
        TargetType = TargetType.SingleEnemy,
        IconLogicalPath = "icons/spells/spark.png",
        Description = "Sort de départ du monde démo.",
    };

    public static ClassDefinition CreateClass() => new()
    {
        Id = ClassId,
        Name = "Villageois",
        Description = "Classe jouable du monde démo.",
        BaseHp = 100,
        BaseMp = 40,
        Str = 10,
        Agi = 10,
        Vit = 10,
        Int = 10,
        Dex = 10,
        Luck = 10,
        StartingSpellId = SpellId,
    };

    public static ShopDefinition CreateShop() => new()
    {
        Id = ShopId,
        Name = "Échoppe du village",
        Description = "Vente d'équipement et de consommables.",
        Listings =
        [
            new ShopListing { ItemId = PotionId, Price = 25, Stock = null },
            new ShopListing { ItemId = SwordId, Price = 120, Stock = null },
            new ShopListing { ItemId = TunicId, Price = 80, Stock = null },
            new ShopListing { ItemId = ClothId, Price = 8, Stock = null },
        ],
    };

    public static ProfessionDefinition CreateProfession() => new()
    {
        Id = ProfessionId,
        Name = "Herboriste",
        MaxLevel = 10,
    };

    public static IReadOnlyList<RecipeDefinition> CreateRecipes() =>
    [
        new RecipeDefinition
        {
            Id = BandageRecipeId,
            Name = "Bandage d'herbes",
            ProfessionId = ProfessionId,
            RequiredProfessionLevel = 1,
            OutputItemId = BandageId,
            OutputQuantity = 1,
            GoldCost = 0,
            ProfessionExperienceReward = 10,
            Ingredients =
            [
                new RecipeIngredientDefinition { ItemId = HerbId, Quantity = 1 },
                new RecipeIngredientDefinition { ItemId = ClothId, Quantity = 1 },
            ],
        },
        new RecipeDefinition
        {
            Id = BreadRecipeId,
            Name = "Pain de seigle",
            ProfessionId = ProfessionId,
            RequiredProfessionLevel = 1,
            OutputItemId = BreadId,
            OutputQuantity = 1,
            GoldCost = 2,
            ProfessionExperienceReward = 8,
            Ingredients =
            [
                new RecipeIngredientDefinition { ItemId = HerbId, Quantity = 1 },
            ],
        },
    ];

    public static DialogueDefinition CreateDialogue() => new()
    {
        Id = DialogueId,
        Name = "Accueil du guide",
        EditorAliasId = 9001,
        Lines =
        [
            new DialogueLineDefinition { Speaker = "Guide", Text = "Bienvenue au village. Veux-tu aider les faubourgs ?" },
        ],
        Choices =
        [
            new DialogueChoiceDefinition
            {
                ChoiceId = "accept",
                Label = "J'accepte la quête",
                StartQuestId = FirstStepsQuestId,
            },
            new DialogueChoiceDefinition
            {
                ChoiceId = "later",
                Label = "Plus tard",
            },
        ],
    };

    public static IReadOnlyList<QuestDefinition> CreateQuests(int outskirtsRuntimeMapId) =>
    [
        new QuestDefinition
        {
            Id = FirstStepsQuestId,
            Name = "Premiers pas",
            EditorAliasId = 9002,
            Stages =
            [
                new QuestStageDefinition
                {
                    Description = "Parler au guide",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Talk,
                            Description = "Parler au guide",
                            RequiredCount = 1,
                            TargetDialogueId = DialogueId,
                        },
                    ],
                },
                new QuestStageDefinition
                {
                    Description = "Visiter les faubourgs",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Visit,
                            Description = "Aller au marqueur des faubourgs",
                            RequiredCount = 1,
                            TargetMapId = outskirtsRuntimeMapId,
                            TargetTileX = VisitTileX,
                            TargetTileY = VisitTileY,
                        },
                    ],
                },
            ],
            CompletionReward = new QuestRewardDefinition { Gold = 25, ItemId = ClothId, ItemQuantity = 1 },
        },
        new QuestDefinition
        {
            Id = TrialQuestId,
            Name = "L'épreuve",
            EditorAliasId = 9003,
            PrerequisiteQuestIds = [FirstStepsQuestId],
            Stages =
            [
                new QuestStageDefinition
                {
                    Description = "Récolter une herbe",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Collect,
                            Description = "Ramasser une herbe",
                            RequiredCount = 1,
                            TargetItemId = HerbId,
                        },
                    ],
                },
                new QuestStageDefinition
                {
                    Description = "Vaincre le loup",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Kill,
                            Description = "Tuer un loup de l'arène",
                            RequiredCount = 1,
                            TargetNpcId = WolfId,
                        },
                    ],
                },
                new QuestStageDefinition
                {
                    Description = "Fabriquer un bandage",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Craft,
                            Description = "Craft bandage",
                            RequiredCount = 1,
                            TargetRecipeId = BandageRecipeId,
                        },
                    ],
                },
            ],
            CompletionReward = new QuestRewardDefinition { Gold = 50, ItemId = ArenaKeyId, ItemQuantity = 1 },
        },
    ];

    public static WeatherProfileDefinition CreateVillageWeather() => new()
    {
        Id = VillageWeatherId,
        Name = "Clair de village",
        WeatherKind = "clear",
        LightingFactor = 1.0f,
    };

    public static WeatherProfileDefinition CreateWildsWeather() => new()
    {
        Id = WildsWeatherId,
        Name = "Pluie des faubourgs",
        WeatherKind = "rain",
        LightingFactor = 0.55f,
    };

    public static RegionDefinition CreateVillageRegion(int villageRuntimeMapId) => new()
    {
        Id = VillageRegionId,
        Name = "Place du village",
        MapId = villageRuntimeMapId,
        TileXMin = 0,
        TileYMin = 0,
        TileXMax = 19,
        TileYMax = 19,
        WeatherProfileId = VillageWeatherId,
    };

    public static RegionDefinition CreateWildsRegion(int outskirtsRuntimeMapId) => new()
    {
        Id = WildsRegionId,
        Name = "Sous-bois",
        MapId = outskirtsRuntimeMapId,
        TileXMin = 10,
        TileYMin = 0,
        TileXMax = 19,
        TileYMax = 19,
        WeatherProfileId = WildsWeatherId,
    };

    public static CommonEventDefinition CreateWelcomeCommonEvent() => new()
    {
        Id = WelcomeCommonEventId,
        Name = "Accueil réutilisable",
        EditorAliasId = 9101,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Le village t'accueille. Parle au guide."}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.SetSwitch,
                        ParameterJson = $$"""{"switchId":"{{WelcomeSwitchId}}","value":true}""",
                    },
                ],
            },
        ],
    };

    public static MapEventDefinition CreateOnceRewardEvent() => new()
    {
        Id = OnceRewardEventId,
        Name = "Coffre unique",
        EditorAliasId = 9102,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.GiveItem,
                        ParameterJson = $$"""{"itemId":"{{PotionId:D}}","quantity":1,"onceKey":"{{OnceRewardKey}}"}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Une potion unique. Elle ne reviendra pas."}""",
                    },
                ],
            },
        ],
    };

    public static MapEventDefinition CreateLearnProfessionEvent() => new()
    {
        Id = LearnProfessionEventId,
        Name = "Apprendre herboriste",
        EditorAliasId = 9103,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.LearnProfession,
                        ParameterJson = $$"""{"professionId":"{{ProfessionId:D}}"}""",
                    },
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Tu apprends le métier d'herboriste."}""",
                    },
                ],
            },
        ],
    };

    public static MapEventDefinition CreateDialogueEvent() => new()
    {
        Id = DialogueEventId,
        Name = "Parler au guide",
        EditorAliasId = 9104,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.StartDialogue,
                        ParameterJson = $$"""{"dialogueId":"{{DialogueId:D}}"}""",
                    },
                ],
            },
        ],
    };

    public static MapEventDefinition CreateCommonCallerEvent() => new()
    {
        Id = CommonCallerEventId,
        Name = "Panneau d'accueil",
        EditorAliasId = 9105,
        Pages =
        [
            new MapEventPageDefinition
            {
                PageOrder = 0,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Conditions =
                [
                    new MapEventConditionDefinition
                    {
                        Kind = MapEventConditionKinds.CharacterSwitch,
                        ParameterJson = $$"""{"switchId":"{{WelcomeSwitchId}}","value":false}""",
                    },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.CallCommonEvent,
                        ParameterJson = $$"""{"commonEventId":"{{WelcomeCommonEventId:D}}"}""",
                    },
                ],
            },
            new MapEventPageDefinition
            {
                PageOrder = 1,
                TriggerKind = Phase8MapEventTriggerKinds.Action,
                BlocksCollision = false,
                Conditions =
                [
                    new MapEventConditionDefinition
                    {
                        Kind = MapEventConditionKinds.CharacterSwitch,
                        ParameterJson = $$"""{"switchId":"{{WelcomeSwitchId}}","value":true}""",
                    },
                ],
                Commands =
                [
                    new MapEventCommandDefinition
                    {
                        Discriminator = MapEventCommandDiscriminators.ShowText,
                        ParameterJson = """{"text":"Tu as déjà lu le panneau."}""",
                    },
                ],
            },
        ],
    };

    public static Map CreateVillageMap(Guid outskirtsMapId) =>
        CreateGrid(VillageMapName, tileSrcX: 0, VillageWarpOutX, VillageWarpOutY, outskirtsMapId, OutskirtsWarpVillageX + 1, OutskirtsWarpVillageY);

    public static Map CreateOutskirtsMap(Guid villageMapId, Guid arenaMapId)
    {
        var map = CreateGrid(OutskirtsMapName, tileSrcX: 1, OutskirtsWarpVillageX, OutskirtsWarpVillageY, villageMapId, VillageWarpOutX - 1, VillageWarpOutY);
        ApplyWarp(map, OutskirtsWarpArenaX, OutskirtsWarpArenaY, arenaMapId, ArenaWarpInX + 1, ArenaWarpInY);
        return map;
    }

    public static Map CreateArenaMap(Guid outskirtsMapId) =>
        CreateGrid(ArenaMapName, tileSrcX: 2, ArenaWarpInX, ArenaWarpInY, outskirtsMapId, OutskirtsWarpArenaX - 1, OutskirtsWarpArenaY);

    public static IReadOnlyList<QuestObjectiveKind> RequiredObjectiveKinds =>
    [
        QuestObjectiveKind.Talk,
        QuestObjectiveKind.Visit,
        QuestObjectiveKind.Collect,
        QuestObjectiveKind.Kill,
        QuestObjectiveKind.Craft,
    ];

    private static Map CreateGrid(
        string name,
        int tileSrcX,
        int warpX,
        int warpY,
        Guid warpTarget,
        int destX,
        int destY)
    {
        var map = new Map { Name = name, Width = 20, Height = 20 };
        var ground = new Layer { LayerType = LayerType.Ground };
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var type = x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1
                    ? TileType.Block
                    : TileType.Ground;
                ground.Tiles.Add(new Tile
                {
                    X = x,
                    Y = y,
                    TilesetId = 1,
                    SrcX = tileSrcX,
                    SrcY = 0,
                    Type = type,
                });
            }
        }

        map.Layers.Add(ground);
        ApplyWarp(map, warpX, warpY, warpTarget, destX, destY);
        return map;
    }

    private static void ApplyWarp(Map map, int x, int y, Guid targetMapId, int destX, int destY)
    {
        var tile = map.Layers[0].Tiles.First(t => t.X == x && t.Y == y);
        tile.Type = TileType.Warp;
        tile.WarpTargetMapId = targetMapId;
        tile.WarpTargetX = destX;
        tile.WarpTargetY = destY;
    }

    private static ItemDefinition Item(
        Guid id,
        string name,
        ItemType kind,
        string icon,
        int stack,
        int buy,
        int sell,
        string description) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
        IconLogicalPath = icon,
        MaxStack = stack,
        BuyPrice = buy,
        SellPrice = sell,
        Description = description,
    };

    private static NpcDefinition Npc(
        Guid id,
        string name,
        NpcKind kind,
        string sprite,
        int level,
        string notes) => new()
    {
        Id = id,
        Name = name,
        Kind = kind,
        SpriteLogicalPath = sprite,
        Level = level,
        Notes = notes,
    };
}
