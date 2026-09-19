using System;
using System.IO;
using System.Linq;
using Frog.Application.Demo;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase10DemoWorldCatalogTests
{
    [Fact]
    public void Catalog_MeetsMandateInventory()
    {
        var items = Phase10DemoWorldCatalog.CreateItems();
        Assert.Equal(8, items.Count);
        Assert.All(items, i => Assert.True(i.Validate(out _)));

        var npcs = Phase10DemoWorldCatalog.CreateFriendlyNpcs();
        Assert.Equal(3, npcs.Count);
        Assert.All(npcs, n => Assert.Equal(NpcKind.Npc, n.Kind));

        var monsters = Phase10DemoWorldCatalog.CreateMonsters();
        Assert.Equal(2, monsters.Count);
        Assert.All(monsters, n => Assert.Equal(NpcKind.Monster, n.Kind));

        var recipes = Phase10DemoWorldCatalog.CreateRecipes();
        Assert.Equal(2, recipes.Count);
        Assert.All(recipes, r => Assert.True(r.Validate(out _)));

        var quests = Phase10DemoWorldCatalog.CreateQuests(2);
        Assert.Equal(2, quests.Count);
        var kinds = quests
            .SelectMany(q => q.Stages)
            .SelectMany(s => s.Objectives)
            .Select(o => o.Kind)
            .Distinct()
            .OrderBy(k => k)
            .ToArray();
        Assert.Equal(Phase10DemoWorldCatalog.RequiredObjectiveKinds.OrderBy(k => k), kinds);

        Assert.True(Phase10DemoWorldCatalog.CreateClass().Validate(out _));
        Assert.True(Phase10DemoWorldCatalog.CreateSpell().Validate(out _));
        Assert.True(Phase10DemoWorldCatalog.CreateProfession().Validate(out _));
        Assert.True(Phase10DemoWorldCatalog.CreateDialogue().Validate(out _));
        Assert.Contains(Phase10DemoWorldCatalog.CreateDialogue().Choices, c => c.StartQuestId == Phase10DemoWorldCatalog.FirstStepsQuestId);
        Assert.True(Phase10DemoWorldCatalog.CreateWelcomeCommonEvent().Validate(out _));
        Assert.True(Phase10DemoWorldCatalog.CreateOnceRewardEvent().Validate(out _));
        Assert.Contains(
            Phase10DemoWorldCatalog.CreateCommonCallerEvent().Pages[0].Conditions,
            c => c.Kind == MapEventConditionKinds.CharacterSwitch);

        var outskirts = Guid.NewGuid();
        var arena = Guid.NewGuid();
        var village = Guid.NewGuid();
        var villageMap = Phase10DemoWorldCatalog.CreateVillageMap(outskirts);
        var outskirtsMap = Phase10DemoWorldCatalog.CreateOutskirtsMap(village, arena);
        var arenaMap = Phase10DemoWorldCatalog.CreateArenaMap(outskirts);
        Assert.True(villageMap.Validate(out _));
        Assert.True(outskirtsMap.Validate(out _));
        Assert.True(arenaMap.Validate(out _));
        Assert.Contains(villageMap.Layers[0].Tiles, t => t.Type == Frog.Core.Enums.TileType.Warp && t.WarpTargetMapId == outskirts);
        Assert.Contains(outskirtsMap.Layers[0].Tiles, t => t.WarpTargetMapId == village);
        Assert.Contains(outskirtsMap.Layers[0].Tiles, t => t.WarpTargetMapId == arena);
        Assert.Contains(arenaMap.Layers[0].Tiles, t => t.WarpTargetMapId == outskirts);
    }

    [Fact]
    public void LicensesFile_ExistsAndDeniesFrogAssets()
    {
        var root = RepoRoot();
        var path = Path.Combine(root, "docs", "progress", "phase-10-beta-release", "demo-world", "LICENSES.md");
        Assert.True(File.Exists(path));
        var text = File.ReadAllText(path);
        Assert.Contains("Aucun asset FRoG", text, StringComparison.Ordinal);
        Assert.Contains("procédurales", text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "scripts", "publish-demo-world.sh")));
        Assert.True(File.Exists(Path.Combine(root, "tools", "Frog.DemoWorld", "Frog.DemoWorld.csproj")));
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
