using System;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class NpcDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_NpcAndMonster()
    {
        var npc = Valid();
        Assert.True(npc.Validate(out var error));
        Assert.Null(error);

        npc.Kind = NpcKind.Monster;
        npc.Level = 99;
        Assert.True(npc.Validate(out error));
        Assert.Null(error);
    }

    [Fact]
    public void Validate_Rejects_InvalidLevelKindAndSpritePath()
    {
        var definition = Valid();
        definition.Level = 0;
        Assert.False(definition.Validate(out _));

        definition = Valid();
        definition.Kind = (NpcKind)99;
        Assert.False(definition.Validate(out _));

        definition = Valid();
        definition.SpriteLogicalPath = "../outside.png";
        Assert.False(definition.Validate(out _));
    }

    [Fact]
    public void Validate_Rejects_InvalidAliasAndLongNotes()
    {
        var definition = Valid();
        definition.EditorAliasId = 0;
        Assert.False(definition.Validate(out _));

        definition = Valid();
        definition.Notes = new string('x', NpcDefinition.MaxNotesLength + 1);
        Assert.False(definition.Validate(out _));
    }

    private static NpcDefinition Valid() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Guide",
        Kind = NpcKind.Npc,
        SpriteLogicalPath = "sprites/npcs/guide.png",
        Level = 1,
        Notes = "Accueille les nouveaux joueurs.",
        EditorAliasId = 1,
    };
}

public sealed class NpcWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_Duplicate_Search_RoundTrip()
    {
        var repository = new InMemoryNpcRepository();
        var session = new NpcWorkspaceSession(repository);
        session.AdoptNewDraft(CreateDefinition(
            "Gobelin",
            NpcKind.Monster,
            "sprites/monsters/goblin.png",
            level: 7,
            alias: 12));

        var saved = Assert.IsType<SaveNpcResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.Current!.Name = "Gobelin publié";
        session.Current.Notes = "Garde la grotte.";
        session.MarkDirty();
        var published = Assert.IsType<SaveNpcResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.Equal(2, published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var draft = await repository.LoadByIdAsync(saved.NpcId);
        var snapshot = await repository.LoadPublishedByIdAsync(saved.NpcId);
        Assert.Equal("Gobelin publié", draft!.Definition.Name);
        Assert.Equal("Garde la grotte.", snapshot!.Definition.Notes);
        Assert.Equal(NpcKind.Monster, snapshot.Definition.Kind);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);
        Assert.Null(session.Current.EditorAliasId);

        session.SearchFilter = "Gobelin";
        await session.RefreshCatalogAsync();
        Assert.Contains(session.Catalog, entry => entry.NpcId == saved.NpcId);
    }

    [Fact]
    public async Task Invalid_CannotPublish_Referenced_CannotDelete()
    {
        var repository = new InMemoryNpcRepository(aliasReferenced: id => id == 3);
        var session = new NpcWorkspaceSession(repository);
        var invalid = CreateDefinition(
            "Trop fort",
            NpcKind.Monster,
            "sprites/monsters/bad.png",
            level: 100);
        session.AdoptNewDraft(invalid);
        Assert.IsType<SaveNpcResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));

        session.AdoptNewDraft(CreateDefinition(
            "Marchand",
            NpcKind.Npc,
            "sprites/npcs/vendor.png",
            level: 4,
            alias: 3));
        var saved = Assert.IsType<SaveNpcResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        await session.OpenAsync(saved.NpcId);
        Assert.IsType<DeleteNpcResult.Referenced>(await session.DeleteCurrentAsync());
    }

    [Fact]
    public async Task DraftDistinctFromPublished_AndStaleRevisionConflicts()
    {
        var repository = new InMemoryNpcRepository();
        var definition = CreateDefinition(
            "Version 1",
            NpcKind.Npc,
            "sprites/npcs/versioned.png",
            level: 2);
        var published = Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            Definition = definition,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        }));

        definition.Name = "Version 2 brouillon";
        Assert.IsType<SaveNpcResult.Success>(await repository.SaveAsync(new SaveNpcRequest
        {
            NpcId = published.NpcId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));

        Assert.Equal(
            "Version 2 brouillon",
            (await repository.LoadByIdAsync(published.NpcId))!.Definition.Name);
        Assert.Equal(
            "Version 1",
            (await repository.LoadPublishedByIdAsync(published.NpcId))!.Definition.Name);

        Assert.IsType<SaveNpcResult.Conflict>(await repository.SaveAsync(new SaveNpcRequest
        {
            NpcId = published.NpcId,
            Definition = definition,
            ExpectedRevision = 1,
            Intent = SaveContentIntent.SaveDraft,
        }));
    }

    internal static NpcDefinition CreateDefinition(
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
        EditorAliasId = alias,
    };
}

public sealed class PublishedNpcConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublishedDefinitions()
    {
        var repository = new InMemoryNpcRepository();
        await repository.SaveAsync(new SaveNpcRequest
        {
            Definition = NpcWorkspaceSessionTests.CreateDefinition(
                "Brouillon",
                NpcKind.Npc,
                "sprites/npcs/draft.png",
                1),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        await repository.SaveAsync(new SaveNpcRequest
        {
            Definition = NpcWorkspaceSessionTests.CreateDefinition(
                "Slime publié",
                NpcKind.Monster,
                "sprites/monsters/slime.png",
                5),
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedNpcConsumer(
            repository,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedNpcConsumer>());
        var loaded = await consumer.LoadPublishedAsync();

        var definition = Assert.Single(loaded);
        Assert.Equal("Slime publié", definition.Name);
        Assert.Equal(NpcKind.Monster, definition.Kind);
    }
}
