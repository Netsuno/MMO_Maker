using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frog.Application.Content;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapObjectPlacementCatalogTests
{
    [Fact]
    public async Task PublishedMapObject_JoinsPlacementCatalog_DraftDoesNot()
    {
        var repo = new InMemoryMapObjectRepository(ContentRepositoryCapabilities.InMemoryTest);
        var draft = Sample("Lampe", "lampe");
        var saved = Assert.IsType<SaveMapObjectResult.Success>(await repo.SaveAsync(new SaveMapObjectRequest
        {
            Definition = draft,
            ExpectedRevision = 0,
            Intent = SaveContentIntent.SaveDraft,
        }));

        var beforePublish = MapObjectPlacementCatalog.Merge(
            BuiltInPrefabCatalog.Create(),
            await repo.ListPublishedAsync());
        Assert.Equal(8, beforePublish.Prefabs.Count);
        Assert.DoesNotContain(beforePublish.Prefabs, p => p.Id == "lampe");

        draft.Name = "Lampe de table";
        var published = Assert.IsType<SaveMapObjectResult.Success>(await repo.SaveAsync(new SaveMapObjectRequest
        {
            MapObjectId = saved.MapObjectId,
            Definition = draft,
            ExpectedRevision = saved.NewRevision,
            Intent = SaveContentIntent.Publish,
        }));
        Assert.NotNull(published.PublishedRevision);

        var catalog = MapObjectPlacementCatalog.Merge(
            BuiltInPrefabCatalog.Create(),
            await repo.ListPublishedAsync());
        Assert.Equal(9, catalog.Prefabs.Count);
        Assert.Contains(catalog.Prefabs, p => p.Id == BuiltInPrefabCatalog.SofaId && p.DisplayName == "Canapé");
        var lamp = Assert.Single(catalog.Prefabs, p => p.Id == "lampe");
        Assert.Equal("Lampe de table", lamp.DisplayName);
        Assert.Equal("lampe.png", lamp.Variants[0].SpriteFileName);

        var placed = new List<PrefabPlacement>();
        Assert.True(
            PrefabPlacementService.TryPlace(
                placed,
                catalog,
                "lampe",
                PrefabFacing.South,
                2,
                2,
                8,
                8,
                out var instance,
                out var error),
            error);
        Assert.Equal("lampe", instance!.PrefabId);
        Assert.Equal(2, instance.TileX);
        Assert.Equal(2, instance.TileY);
    }

    [Fact]
    public void CreatePlacementId_IsPlaceable()
    {
        var id = MapObjectDefinition.CreatePlacementId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        Assert.True(PrefabPlacementService.IsValidId(id));
        var def = Sample("Coffre peint", id);
        def.PlacementId = id;
        def.LogicalPath = $"prefabs/{id}.png";
        Assert.True(def.Validate(out var error), error);
        Assert.False(new MapObjectDefinition
        {
            Id = Guid.NewGuid(),
            Name = "X",
            LogicalPath = "prefabs/x.png",
            PlacementId = "Lampe",
            FootprintWidthTiles = 1,
            FootprintHeightTiles = 1,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('b', 64),
        }.Validate(out _));
    }

    [Fact]
    public async Task DuplicateLogicalPath_IsRejected()
    {
        var repo = new InMemoryMapObjectRepository(ContentRepositoryCapabilities.InMemoryTest);
        Assert.IsType<SaveMapObjectResult.Success>(await repo.SaveAsync(new SaveMapObjectRequest
        {
            Definition = Sample("Lampe", "lampe"),
            ExpectedRevision = 0,
        }));

        var duplicate = Sample("Autre", "autre");
        duplicate.LogicalPath = "prefabs/lampe.png";
        var rejected = await repo.SaveAsync(new SaveMapObjectRequest
        {
            Definition = duplicate,
            ExpectedRevision = 0,
        });
        Assert.IsType<SaveMapObjectResult.ValidationFailed>(rejected);
    }

    private static MapObjectDefinition Sample(string name, string placementId)
    {
        var id = Guid.NewGuid();
        return new MapObjectDefinition
        {
            Id = id,
            Name = name,
            LogicalPath = $"prefabs/{placementId}.png",
            PlacementId = placementId,
            FootprintWidthTiles = 1,
            FootprintHeightTiles = 1,
            WidthPixels = 32,
            HeightPixels = 32,
            Sha256Hex = new string('a', 64),
            PngBytes = [9, 8, 7],
        };
    }
}
