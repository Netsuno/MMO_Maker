using System;
using System.Collections.Generic;
using Frog.Application.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapPlacedEntityTests
{
    [Fact]
    public void Place_ThreeKinds_OnDistinctTiles_WithoutMapId()
    {
        var map = Map(8, 6);
        var entities = new List<MapPlacedEntity>();

        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Spawn, 1, 1, out var spawn, out var spawnCreated));
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Npc, 2, 3, out var npc, out var npcCreated));
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Object, 4, 5, out var obj, out var objCreated));

        Assert.True(spawnCreated);
        Assert.True(npcCreated);
        Assert.True(objCreated);
        Assert.Equal(3, entities.Count);
        Assert.Equal("Apparition 1", spawn.Name);
        Assert.Equal("PNJ 1", npc.Name);
        Assert.Equal("Objet 1", obj.Name);
        Assert.Equal(MapPlacedFacing.South, npc.Facing);
        Assert.Equal(1, npc.Level);
        Assert.Equal(0, spawn.RespawnSeconds);
        Assert.NotEqual(Guid.Empty, spawn.Id);
        Assert.NotEqual(spawn.Id, npc.Id);
    }

    [Fact]
    public void Place_OccupiedTile_SelectsExisting_AndDoesNotDuplicate()
    {
        var map = Map(4, 4);
        var entities = new List<MapPlacedEntity>();
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Npc, 1, 1, out var first, out var created));
        Assert.True(created);

        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Object, 1, 1, out var again, out var createdAgain));
        Assert.False(createdAgain);
        Assert.Same(first, again);
        Assert.Equal(MapPlacedKind.Npc, again.Kind);
        Assert.Single(entities);
    }

    [Fact]
    public void Place_RejectsOutOfBounds_AndDoesNotClamp()
    {
        var map = Map(3, 3);
        var entities = new List<MapPlacedEntity>();
        Assert.False(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Spawn, -1, 0, out _, out _));
        Assert.False(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Spawn, 3, 0, out _, out _));
        Assert.False(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Npc, 0, 9, out _, out _));
        Assert.Empty(entities);
    }

    [Fact]
    public void RemoveAndMove_RespectOneEntityPerTile()
    {
        var map = Map(6, 6);
        var entities = new List<MapPlacedEntity>();
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Npc, 1, 1, out var npc, out _));
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Object, 2, 2, out var obj, out _));

        Assert.False(MapPlacedEntityEdit.TryMove(entities, map, npc.Id, 2, 2));
        Assert.Equal((1, 1), (npc.TileX, npc.TileY));

        Assert.True(MapPlacedEntityEdit.TryMove(entities, map, npc.Id, 3, 4));
        Assert.Equal((3, 4), (npc.TileX, npc.TileY));
        Assert.False(MapPlacedEntityEdit.TryMove(entities, map, npc.Id, 9, 0));

        Assert.True(MapPlacedEntityEdit.TryRemoveAt(entities, 2, 2, out var removed));
        Assert.Equal(obj.Id, removed!.Id);
        Assert.Single(entities);
        Assert.False(MapPlacedEntityEdit.TryRemoveAt(entities, 2, 2, out _));
    }

    [Fact]
    public void Apply_UpdatesProperties_AndRejectsInvalidText()
    {
        var map = Map(5, 5);
        var entities = new List<MapPlacedEntity>();
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Spawn, 0, 0, out var entity, out _));

        Assert.True(MapPlacedEntityEdit.TryApply(
            entity,
            map,
            MapPlacedKind.Npc,
            "  Garde  ",
            "Parle au joueur",
            MapPlacedFacing.East,
            12,
            7,
            out var error));
        Assert.Null(error);
        Assert.Equal(MapPlacedKind.Npc, entity.Kind);
        Assert.Equal("Garde", entity.Name);
        Assert.Equal("Parle au joueur", entity.Notes);
        Assert.Equal(MapPlacedFacing.East, entity.Facing);
        Assert.Equal(12, entity.RespawnSeconds);
        Assert.Equal(7, entity.Level);
        Assert.Contains("PNJ « Garde »", MapPlacedEntityEdit.FormatSummary(entity), StringComparison.Ordinal);
        Assert.Contains("Est", MapPlacedEntityEdit.FormatSummary(entity), StringComparison.Ordinal);

        Assert.False(MapPlacedEntityEdit.TryApply(entity, map, MapPlacedKind.Npc, " ", "x", MapPlacedFacing.South, 0, 1, out error));
        Assert.Contains("Nom", error, StringComparison.Ordinal);
        Assert.Equal("Garde", entity.Name);

        Assert.False(MapPlacedEntityEdit.TryApply(entity, map, MapPlacedKind.Spawn, "Camp", "", MapPlacedFacing.South, -1, 1, out error));
        Assert.Contains("Réapparition", error, StringComparison.Ordinal);
        Assert.Equal(12, entity.RespawnSeconds);

        Assert.False(MapPlacedEntityEdit.TryApply(entity, map, MapPlacedKind.Npc, "Camp", "", MapPlacedFacing.South, 0, 0, out error));
        Assert.Contains("Niveau", error, StringComparison.Ordinal);

        Assert.False(MapPlacedEntityEdit.TryApply(
            entity,
            map,
            MapPlacedKind.Object,
            "Coffre",
            new string('n', MapPlacedEntityEdit.MaxNotesLength + 1),
            MapPlacedFacing.North,
            0,
            1,
            out error));
        Assert.Contains("Notes", error, StringComparison.Ordinal);
        Assert.Equal("Garde", entity.Name);
    }

    [Fact]
    public void DropOutside_RemovesEntitiesAfterShrink_CloneIsIndependent()
    {
        var map = Map(4, 4);
        var entities = new List<MapPlacedEntity>();
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Object, 3, 3, out _, out _));
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Spawn, 0, 1, out var kept, out _));

        var clone = MapPlacedEntityEdit.Clone(entities);
        clone[0].Name = "modifié";
        Assert.NotEqual("modifié", entities[0].Name);

        map.Width = 2;
        map.Height = 2;
        Assert.Equal(1, MapPlacedEntityEdit.DropOutside(entities, map));
        var remaining = Assert.Single(entities);
        Assert.Equal(kept.Id, remaining.Id);
        Assert.Equal((0, 1), (remaining.TileX, remaining.TileY));
    }

    [Theory]
    [InlineData(MapPlacedKind.Spawn, "Apparition")]
    [InlineData(MapPlacedKind.Npc, "PNJ")]
    [InlineData(MapPlacedKind.Object, "Objet")]
    public void KindLabels_AreFrench(MapPlacedKind kind, string label)
    {
        Assert.Equal(label, MapPlacedEntityEdit.KindLabel(kind));
    }

    private static Map Map(int width, int height) => new()
    {
        Name = "test",
        Width = width,
        Height = height,
    };
}
