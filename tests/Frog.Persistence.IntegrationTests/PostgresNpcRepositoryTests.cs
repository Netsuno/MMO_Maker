using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Persistence.PostgreSql;
using Frog.Persistence.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace Frog.Persistence.IntegrationTests;

[Collection("PostgresIsolated")]
public sealed class PostgresNpcRepositoryTests
{
    private readonly IsolatedPostgresFixture _fixture;

    public PostgresNpcRepositoryTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Save_Publish_Reload_DraftDistinct_Conflict_InvalidPublish_Rollback()
    {
        using var gate = CreateGate();
        var repository = new PostgresNpcRepository(gate);
        var definition = CreateDefinition(
            "Gobelin",
            NpcKind.Monster,
            "sprites/monsters/goblin.png",
            level: 8,
            alias: 8101);

        var created = Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));
        Assert.Equal(1, created.NewRevision);

        definition.Name = "Gobelin publié";
        definition.Notes = "Patrouille la grotte.";
        var published = Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            NpcId = created.NpcId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Equal(2, published.NewRevision);
        Assert.Equal(2, published.PublishedRevision);

        using var gate2 = CreateGate();
        var repository2 = new PostgresNpcRepository(gate2);
        var draft = await repository2.LoadByIdAsync(created.NpcId);
        var snapshot = await repository2.LoadPublishedByIdAsync(created.NpcId);
        Assert.NotNull(draft);
        Assert.NotNull(snapshot);
        AssertDefinitionEqual(definition, draft!.Definition);
        AssertDefinitionEqual(definition, snapshot!.Definition);

        draft.Definition.Name = "Gobelin brouillon";
        draft.Definition.Level = 9;
        Assert.IsType<SaveNpcResult.Success>(await repository2.SaveAsync(new SaveNpcRequest
        {
            NpcId = created.NpcId,
            Definition = draft.Definition,
            ExpectedRevision = draft.Revision,
            Intent = SaveContentIntent.SaveDraft,
        }));

        using var gate3 = CreateGate();
        var repository3 = new PostgresNpcRepository(gate3);
        Assert.Equal("Gobelin brouillon", (await repository3.LoadByIdAsync(created.NpcId))!.Definition.Name);
        Assert.Equal("Gobelin publié", (await repository3.LoadPublishedByIdAsync(created.NpcId))!.Definition.Name);

        Assert.IsType<SaveNpcResult.Conflict>(await repository3.SaveAsync(new SaveNpcRequest
        {
            NpcId = created.NpcId,
            Definition = draft.Definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var invalid = CreateDefinition(
            "Niveau invalide",
            NpcKind.Monster,
            "sprites/monsters/invalid.png",
            level: 100);
        Assert.IsType<SaveNpcResult.ValidationFailed>(await repository3.SaveAsync(new SaveNpcRequest
        {
            Definition = invalid,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        using var gate4 = CreateGate();
        var failing = new PostgresNpcRepository(gate4)
        {
            TestBeforeCommitAsync = _ => throw new InvalidOperationException("injected-fail"),
        };
        var before = await failing.ListSummariesAsync();
        var failedResult = await failing.SaveAsync(new SaveNpcRequest
        {
            Definition = CreateDefinition(
                "Publication annulée",
                NpcKind.Npc,
                "sprites/npcs/rollback.png",
                level: 3,
                alias: 8102),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });
        Assert.IsType<SaveNpcResult.PersistenceFailed>(failedResult);

        using var gate5 = CreateGate();
        var afterRepository = new PostgresNpcRepository(gate5);
        Assert.Empty(await afterRepository.ListSummariesAsync(search: "Publication annulée"));
        Assert.Equal(before.Count, (await afterRepository.ListSummariesAsync()).Count);
        Assert.Contains(
            await afterRepository.ListPublishedAsync(),
            npc => npc.Id == created.NpcId && npc.Name == "Gobelin publié");
    }

    [PostgresFact]
    [Trait("Category", "PostgreSql")]
    public async Task Search_Filter_DeleteBlockedWhenMapReferencesAlias()
    {
        using var gate = CreateGate();
        var repository = new PostgresNpcRepository(gate);
        var definition = CreateDefinition(
            "Marchand référencé",
            NpcKind.Npc,
            "sprites/npcs/vendor.png",
            level: 4,
            alias: 8201);
        var saved = Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var mapId = Guid.NewGuid();
        gate.Db.Maps.Add(new MapEntity
        {
            Id = mapId,
            Name = "Carte NPC",
            Width = 5,
            Height = 5,
            Status = Frog.Application.Maps.MapPublishStatus.Draft,
            Revision = 1,
            LayersCatalogJson = "[]",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        gate.Db.MapNpcSpawns.Add(new MapNpcSpawnEntity
        {
            Id = Guid.NewGuid(),
            MapId = mapId,
            NpcDefinitionId = 8201,
            X = 2,
            Y = 3,
            Direction = 1,
        });
        await gate.Db.SaveChangesAsync();

        var catalog = await repository.ListSummariesAsync(search: "référencé");
        Assert.Contains(catalog, entry => entry.NpcId == saved.NpcId);
        Assert.True(await repository.IsAliasIdReferencedByMapsAsync(8201));
        Assert.IsType<DeleteNpcResult.Referenced>(await repository.DeleteAsync(saved.NpcId));

        Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            NpcId = saved.NpcId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.Contains(await repository.ListPublishedAsync(), npc => npc.Id == saved.NpcId);
    }

    private FrogDbContextGate CreateGate()
        => new(new(FrogDbContextOptions.Create(_fixture.ConnectionString)));

    private static NpcDefinition CreateDefinition(
        string name,
        NpcKind kind,
        string spritePath,
        int level,
        int? alias = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Kind = kind,
        SpriteLogicalPath = spritePath,
        Level = level,
        Notes = "Notes de test",
        EditorAliasId = alias,
    };

    private static void AssertDefinitionEqual(NpcDefinition expected, NpcDefinition actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.SpriteLogicalPath, actual.SpriteLogicalPath);
        Assert.Equal(expected.Level, actual.Level);
        Assert.Equal(expected.Notes, actual.Notes);
        Assert.Equal(expected.EditorAliasId, actual.EditorAliasId);
    }
}
