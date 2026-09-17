using System;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Core.Models;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Frog.Tests;

public sealed class TilesetDefinitionValidationTests
{
    [Fact]
    public void Validate_Accepts_MinimalValidDefinition()
    {
        var def = Valid();
        Assert.True(def.Validate(out var err));
        Assert.Null(err);
    }

    [Fact]
    public void Validate_Rejects_BadPathAndSha()
    {
        var def = Valid();
        def.LogicalPath = "../evil.png";
        Assert.False(def.Validate(out _));

        def = Valid();
        def.Sha256Hex = "abc";
        Assert.False(def.Validate(out _));
    }

    [Fact]
    public void Validate_Rejects_NonMultipleDimensions()
    {
        var def = Valid();
        def.WidthPixels = 33;
        Assert.False(def.Validate(out var err));
        Assert.Contains("multiples", err, StringComparison.OrdinalIgnoreCase);
    }

    private static TilesetDefinition Valid() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Grass",
        LogicalPath = "tiles/grass.png",
        TileSizePixels = 32,
        WidthPixels = 64,
        HeightPixels = 64,
        Sha256Hex = new string('A', 64),
        EditorPaletteId = 1,
    };
}

public sealed class TilesetWorkspaceSessionTests
{
    [Fact]
    public async Task Create_SaveDraft_Publish_Duplicate_Search_RoundTrip()
    {
        var repo = new InMemoryTilesetRepository();
        var session = new TilesetWorkspaceSession(repo);

        var def = new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Alpha",
            LogicalPath = "tiles/alpha.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('1', 64),
            EditorPaletteId = 7,
        };
        session.AdoptNewDraft(def);
        var saved = Assert.IsType<SaveTilesetResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        Assert.Equal(1, saved.NewRevision);
        Assert.Equal(ContentPublishStatus.Draft, session.CurrentStatus);

        session.MarkDirty();
        session.Current!.Name = "Alpha publié";
        var published = Assert.IsType<SaveTilesetResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));
        Assert.NotNull(published.PublishedRevision);
        Assert.Equal(ContentPublishStatus.Published, session.CurrentStatus);

        var draftLoaded = await repo.LoadByIdAsync(saved.TilesetId);
        var pubLoaded = await repo.LoadPublishedByIdAsync(saved.TilesetId);
        Assert.NotNull(draftLoaded);
        Assert.NotNull(pubLoaded);
        Assert.Equal("Alpha publié", draftLoaded!.Definition.Name);
        Assert.Equal("Alpha publié", pubLoaded!.Definition.Name);

        session.DuplicateCurrent();
        Assert.True(session.IsDirty);
        Assert.Contains("(copie)", session.Current!.Name, StringComparison.Ordinal);
        Assert.Null(session.Current.EditorPaletteId);

        session.SearchFilter = "Alpha";
        await session.RefreshCatalogAsync();
        Assert.Contains(session.Catalog, e => e.Name.Contains("Alpha", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_CannotPublish_Referenced_CannotDelete()
    {
        var repo = new InMemoryTilesetRepository(paletteReferenced: id => id == 3);
        var session = new TilesetWorkspaceSession(repo);
        session.AdoptNewDraft(new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Bad",
            LogicalPath = "tiles/bad.png",
            TileSizePixels = 32,
            WidthPixels = 31,
            HeightPixels = 32,
            Sha256Hex = new string('2', 64),
        });
        Assert.IsType<SaveTilesetResult.ValidationFailed>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish));

        session.AdoptNewDraft(new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Used",
            LogicalPath = "tiles/used.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('3', 64),
            EditorPaletteId = 3,
        });
        var ok = Assert.IsType<SaveTilesetResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.SaveDraft));
        await session.OpenAsync(ok.TilesetId);
        Assert.IsType<DeleteTilesetResult.Referenced>(await session.DeleteCurrentAsync());
    }

    [Fact]
    public async Task DraftDistinctFromPublished_AfterEditWithoutPublish()
    {
        var repo = new InMemoryTilesetRepository();
        var session = new TilesetWorkspaceSession(repo);
        session.AdoptNewDraft(new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "V1",
            LogicalPath = "tiles/v.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('4', 64),
        });
        var id = Assert.IsType<SaveTilesetResult.Success>(
            await session.SaveCurrentAsync(SaveContentIntent.Publish)).TilesetId;

        session.Current!.Name = "V2-draft";
        session.MarkDirty();
        await session.SaveCurrentAsync(SaveContentIntent.SaveDraft);

        var draft = await repo.LoadByIdAsync(id);
        var published = await repo.LoadPublishedByIdAsync(id);
        Assert.Equal("V2-draft", draft!.Definition.Name);
        Assert.Equal("V1", published!.Definition.Name);
    }
}

public sealed class PublishedTilesetConsumerTests
{
    [Fact]
    public async Task Consumer_Loads_OnlyPublished()
    {
        var repo = new InMemoryTilesetRepository();
        var draftOnly = new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "DraftOnly",
            LogicalPath = "tiles/d.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('5', 64),
        };
        await repo.SaveAsync(new SaveTilesetRequest
        {
            Definition = draftOnly,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        });
        var pub = new TilesetDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Published",
            LogicalPath = "tiles/p.png",
            TileSizePixels = 32,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('6', 64),
        };
        await repo.SaveAsync(new SaveTilesetRequest
        {
            Definition = pub,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        using var loggerFactory = LoggerFactory.Create(_ => { });
        var consumer = new Frog.Server.Services.PublishedTilesetConsumer(
            repo,
            loggerFactory.CreateLogger<Frog.Server.Services.PublishedTilesetConsumer>());
        var loaded = await consumer.LoadPublishedAsync();
        Assert.Single(loaded);
        Assert.Equal("Published", loaded[0].Name);
    }
}
