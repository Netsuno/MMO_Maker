using System;
using System.IO;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

/// <summary>
/// Catalogue compétences (Données de jeu). Même dépôt que les sorts, type Skill.
/// Hello reste 11. Tuiles TileAsset restent 48.
/// </summary>
public sealed class SkillContentTests
{
    [Fact]
    public void Protocol_Stays11_TileAssetStays48_AndSkillsCategoryExists()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var root = RepoRoot();
        var protocol = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "FrogWireProtocol.cs"));
        var tiles = File.ReadAllText(Path.Combine(root, "Frog.Core", "Constants", "TileAssetMetrics.cs"));
        var form = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "GameDataForm.cs"));
        var panel = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Forms", "GameData", "SkillEditorPanel.cs"));
        Assert.Contains("Version = 11", protocol, StringComparison.Ordinal);
        Assert.DoesNotContain("Version = 12", protocol, StringComparison.Ordinal);
        Assert.Contains("TargetTileSizePixels = 48", tiles, StringComparison.Ordinal);
        Assert.Contains("\"Compétences\"", form, StringComparison.Ordinal);
        Assert.Contains("SkillEditorPanel", form, StringComparison.Ordinal);
        Assert.Contains("Coût en PM", panel, StringComparison.Ordinal);
        Assert.Contains("SpellKind.Skill", panel, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetLabels_RoundTrip_FrenchScopeNames()
    {
        foreach (var target in Enum.GetValues<TargetType>())
        {
            var label = TargetTypeLabels.French(target);
            Assert.True(TargetTypeLabels.TryParse(label, out var parsed));
            Assert.Equal(target, parsed);
        }

        Assert.Equal("Utilisateur", TargetTypeLabels.French(TargetType.Self));
        Assert.Equal("Un ennemi", TargetTypeLabels.French(TargetType.SingleEnemy));
        Assert.Equal("Un allié", TargetTypeLabels.French(TargetType.SingleAlly));
        Assert.Equal("Zone", TargetTypeLabels.French(TargetType.AoE));
        Assert.False(TargetTypeLabels.TryParse("SingleEnemy", out _));
    }

    [Fact]
    public async Task KindFilter_ListsOnlySkills_AndForcesSkillOnSave()
    {
        var repository = new InMemorySpellRepository();
        await repository.SaveAsync(new SaveSpellRequest
        {
            Definition = Definition("Boule de feu", SpellKind.Spell, TargetType.SingleEnemy, 20, 1000),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        var session = new SpellWorkspaceSession(repository) { KindFilter = SpellKind.Skill };
        session.AdoptNewDraft(Definition("Entaille", SpellKind.Spell, TargetType.SingleEnemy, 12, 800));
        Assert.Equal(SpellKind.Skill, session.Current!.Kind);
        session.Current.Description = "Entaille rapide.";

        var published = Assert.IsType<SaveSpellResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        await session.RefreshCatalogAsync();
        var entry = Assert.Single(session.Catalog);
        Assert.Equal("Entaille", entry.Name);
        Assert.Equal(SpellKind.Skill, entry.Kind);
        Assert.Equal(12, entry.ManaCost);
        Assert.Equal(800, entry.CooldownMs);
        Assert.Equal(TargetType.SingleEnemy, entry.TargetType);

        var stored = await repository.LoadPublishedByIdAsync(published.SpellId);
        Assert.Equal(SpellKind.Skill, stored!.Definition.Kind);
        Assert.Equal("Entaille rapide.", stored.Definition.Description);

        var unfiltered = new SpellWorkspaceSession(repository);
        await unfiltered.RefreshCatalogAsync();
        Assert.Equal(2, unfiltered.Catalog.Count);
    }

    [Fact]
    public async Task DraftPublishDuplicateDelete_RoundTrip_AndClassReferenceBlocksDelete()
    {
        var repository = new InMemorySpellRepository();
        var session = new SpellWorkspaceSession(repository) { KindFilter = SpellKind.Skill };
        session.AdoptNewDraft(Definition("Frappe", SpellKind.Skill, TargetType.AoE, 0, 400));
        var draft = Assert.IsType<SaveSpellResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.ManaCost = 5;
        session.MarkDirty();
        var published = Assert.IsType<SaveSpellResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(draft.SpellId, published.SpellId);
        Assert.Equal(5, (await repository.LoadPublishedByIdAsync(published.SpellId))!.Definition.ManaCost);

        session.DuplicateCurrent();
        Assert.Equal(SpellKind.Skill, session.Current!.Kind);
        Assert.Contains("(copie)", session.Current.Name, StringComparison.Ordinal);
        Assert.IsType<SaveSpellResult.Success>(await session.SaveCurrentAsync(SaveContentIntent.Publish));

        var classes = new InMemoryClassRepository(repository);
        Assert.IsType<SaveClassResult.Success>(await classes.SaveAsync(new SaveClassRequest
        {
            Definition = ClassWorkspaceSessionTests.CreateDefinition("Guerrier", published.SpellId),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        var blocked = new SpellWorkspaceSession(repository) { KindFilter = SpellKind.Skill };
        Assert.True(await blocked.OpenAsync(published.SpellId));
        Assert.IsType<DeleteSpellResult.Referenced>(await blocked.DeleteCurrentAsync());
        Assert.NotNull(await repository.LoadByIdAsync(published.SpellId));
    }

    private static SpellDefinition Definition(
        string name,
        SpellKind kind,
        TargetType target,
        int manaCost,
        int cooldownMs) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        ManaCost = manaCost,
        CooldownMs = cooldownMs,
        TargetType = target,
        IconLogicalPath = $"icons/skills/{Guid.NewGuid():N}.png",
        Description = "Description de test",
    };

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

        throw new InvalidOperationException("Frog.Creator.sln introuvable.");
    }
}
