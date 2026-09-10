using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Gameplay;

namespace Frog.Editor.WindowsSmokeTests;

/// <summary>Enregistre le contenu Phase 8 minimal pour les smokes client in-memory.</summary>
internal static class Phase8SmokeContentRegistrar
{
    public static void Register(Phase8InMemoryPublishedContent content)
    {
        var opts = new Phase8SmokeBootstrapOptions();
        var consumableId = Phase7ContentSeed.CreateDefaultConsumable().Id;

        content.RegisterDialogue(new DialogueDefinition
        {
            Id = opts.DialogueId,
            Name = "SmokeGuide",
            Lines = [new DialogueLineDefinition { Speaker = "Guide", Text = "Will you help?" }],
            Choices =
            [
                new DialogueChoiceDefinition
                {
                    ChoiceId = "accept",
                    Label = "Accept",
                    StartQuestId = opts.QuestId,
                },
            ],
        });

        content.RegisterQuest(new QuestDefinition
        {
            Id = opts.QuestId,
            Name = "Smoke Quest",
            Stages =
            [
                new QuestStageDefinition
                {
                    Description = "Talk to guide",
                    Objectives =
                    [
                        new QuestObjectiveDefinition
                        {
                            Kind = QuestObjectiveKind.Talk,
                            Description = "Talk to guide",
                            TargetDialogueId = opts.DialogueId,
                            RequiredCount = 1,
                        },
                    ],
                },
            ],
        });

        content.RegisterProfession(new ProfessionDefinition
        {
            Id = opts.ProfessionId,
            Name = "Alchemist",
            MaxLevel = 10,
        });

        content.RegisterRecipe(new RecipeDefinition
        {
            Id = opts.RecipeId,
            Name = "Smoke Potion",
            ProfessionId = opts.ProfessionId,
            RequiredProfessionLevel = 1,
            OutputItemId = consumableId,
            OutputQuantity = 1,
            Ingredients = [new RecipeIngredientDefinition { ItemId = consumableId, Quantity = 1 }],
            GoldCost = 0,
            ProfessionExperienceReward = 10,
        });

        content.RegisterRegion(new RegionDefinition
        {
            Id = opts.RegionId,
            Name = "Smoke Region",
            MapId = 1,
            TileXMin = 0,
            TileYMin = 0,
            TileXMax = 20,
            TileYMax = 20,
            WeatherProfileId = opts.WeatherProfileId,
        });

        content.RegisterWeather(new WeatherProfileDefinition
        {
            Id = opts.WeatherProfileId,
            Name = "Smoke Weather",
            LightingFactor = 0.7f,
        });
    }
}
