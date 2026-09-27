using System;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class ScrollMapCommandTests
{
    [Fact]
    public void Palette_DefilerLaCarte_DefaultsDownOneTileAtNormalSpeed()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ScrollMapId && entry.Label == "Défiler la carte");
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ScrollMapId, out var command));
        Assert.Equal(MapEventCommandDiscriminators.ScrollMap, command.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
        Assert.True(MapEventParameterSchemas.TryParseScrollMap(command.ParameterJson, out var parsed, out var parseErr), parseErr);
        Assert.Equal(MapEventScroll.DirectionDown, parsed.Direction);
        Assert.Equal(MapEventScroll.DefaultDistance, parsed.Distance);
        Assert.Equal(MapEventScroll.DefaultSpeed, parsed.Speed);
        Assert.Equal(MapEventScroll.MillisecondsPerTile(MapEventScroll.DefaultSpeed), parsed.DurationMs);
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(command.Discriminator));
        Assert.True(Frog.Server.Gameplay.MapEventExecutionPlanner.CanExecuteTransactionally([command]));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
    }

    [Theory]
    [InlineData("""{"direction":"north","distance":1,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":0,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":101,"speed":4}""")]
    [InlineData("""{"direction":"up","distance":1,"speed":0}""")]
    [InlineData("""{"direction":"up","distance":1,"speed":7}""")]
    [InlineData("""{"direction":"left","distance":2}""")]
    [InlineData("""{"direction":"right","distance":1.5,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":1,"speed":4,"wait":true}""")]
    public void Validator_RejectsScrollMap(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ScrollMap,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_NameDirectionDistanceAndSpeed()
    {
        Assert.Equal("Défiler la carte", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ScrollMap));
        Assert.Equal("Direction", MapEventEditorLabels.Field("direction"));
        Assert.Equal("Distance (tuiles)", MapEventEditorLabels.Field("distance"));
        Assert.Equal("Vitesse", MapEventEditorLabels.Field("speed"));
        Assert.Equal("Haut", MapEventEditorLabels.ScrollDirection(MapEventScroll.DirectionUp));
        Assert.Equal("Bas", MapEventEditorLabels.ScrollDirection(MapEventScroll.DirectionDown));
        Assert.Equal("Gauche", MapEventEditorLabels.ScrollDirection(MapEventScroll.DirectionLeft));
        Assert.Equal("Droite", MapEventEditorLabels.ScrollDirection(MapEventScroll.DirectionRight));
        Assert.Equal(
            "Défiler la carte : Bas, 1 tuile, vitesse 4",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ScrollMap,
                """{"direction":"down","distance":1,"speed":4}"""));
        Assert.Equal(
            "Défiler la carte : Gauche, 4 tuiles, vitesse 2",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ScrollMap,
                """{"direction":"left","distance":4,"speed":2}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesScrollWithoutANewOpcode()
    {
        var down = MapEventScrollOp.Create(MapEventScroll.DirectionDown, 3, 4);
        var flash = MapEventScreenOp.ForFlash(255, 255, 255, 170, 200);
        var right = MapEventScrollOp.Create(MapEventScroll.DirectionRight, 1, 6);
        var visuals = new[]
        {
            MapEventVisualOp.ForScroll(down),
            MapEventVisualOp.ForScreen(flash),
            MapEventVisualOp.ForScroll(right),
        };

        var message = MapEventScreenWire.Compose(visuals, null, "Plus bas.", "Nom (slug)");
        Assert.StartsWith("scroll:down:3:4\n", message, StringComparison.Ordinal);
        Assert.Contains("flash:255:255:255:170:200\n", message, StringComparison.Ordinal);
        Assert.Contains("scroll:right:1:6\n", message, StringComparison.Ordinal);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(message, out var ops, out var after));
        Assert.Equal(3, ops.Count);
        Assert.Equal(down, ops[0].Scroll);
        Assert.Equal(flash, ops[1].Screen);
        Assert.Equal(right, ops[2].Scroll);
        Assert.Equal("Plus bas.", after);
        Assert.False(MapEventScroll.TryParseLine("scroll:down:3", out _));
        Assert.False(MapEventScroll.TryParseLine("scroll:north:1:4", out _));
        Assert.False(MapEventScroll.TryParseLine("scroll:up:0:4", out _));
        Assert.False(MapEventScroll.TryParseLine("scroll:left:1:9", out _));

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Playback_MovesByTilesThenStays_AndCommitDoesNotTouchThePlayer()
    {
        const int tile = 48;
        Assert.Equal(tile, TileAssetMetrics.TargetTileSizePixels);
        var down = MapEventScrollOp.Create(MapEventScroll.DirectionDown, 2, 4);
        Assert.Equal(128, down.DurationMs);
        Assert.Equal((0, 0), MapEventScrollPlayback.Sample(down, 0, 0, 0, tile));
        Assert.Equal((0, tile), MapEventScrollPlayback.Sample(down, 0, 0, 64, tile));
        Assert.Equal((0, tile * 2), MapEventScrollPlayback.Sample(down, 0, 0, 128, tile));
        Assert.Equal((0, tile * 2), MapEventScrollPlayback.Sample(down, 0, 0, 400, tile));

        var left = MapEventScrollOp.Create(MapEventScroll.DirectionLeft, 1, 6);
        var settled = MapEventScrollPlayback.Sample(left, 0, tile * 2, left.DurationMs, tile);
        Assert.Equal((-tile, tile * 2), settled);
        Assert.Equal(16, MapEventScroll.MillisecondsPerTile(6));
        Assert.Equal(512, MapEventScroll.MillisecondsPerTile(1));
        Assert.Equal((tile, 0), MapEventScrollPlayback.DeltaPixels(
            MapEventScrollOp.Create(MapEventScroll.DirectionRight, 1, 4),
            tile));
        Assert.Equal((0, -tile), MapEventScrollPlayback.DeltaPixels(
            MapEventScrollOp.Create(MapEventScroll.DirectionUp, 1, 4),
            tile));

        var sandbox = new MapEventTransactionalCommitSandbox();
        var identity = new MapEventExecutionIdentity(Guid.NewGuid(), Guid.NewGuid(), 1, 84);
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ScrollMap,
            ParameterJson = """{"direction":"down","distance":2,"speed":4}""",
        };
        var unit = MapEventExecutionPlan.Ok(identity, [command]).AsTransactionalUnit();
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Equal(0, sandbox.World.ScreenFade);
    }
}
