using System;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class ScrollMapCommandTests
{
    [Fact]
    public void Palette_FaireDefiler_DefaultsDownOneTileAtNormalSpeed()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ScrollMapId && entry.Label == "Faire défiler la carte");
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ScrollMapId, out var command));
        Assert.Equal(MapEventCommandDiscriminators.ScrollMap, command.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
        Assert.True(MapEventParameterSchemas.TryParseScrollMap(command.ParameterJson, out var parsed, out var parseErr), parseErr);
        Assert.True(parsed.IsScroll);
        Assert.Equal(MapEventScroll.Down, parsed.Direction);
        Assert.Equal(MapEventScroll.DefaultDistance, parsed.Distance);
        Assert.Equal(MapEventScroll.DefaultSpeed, parsed.Speed);
        Assert.Equal(MapEventScroll.DurationMs(1, 4), parsed.DurationMs);
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(command.Discriminator));
        Assert.True(Frog.Server.Gameplay.MapEventExecutionPlanner.CanExecuteTransactionally([command]));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
    }

    [Theory]
    [InlineData("""{"direction":"north","distance":1,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":-1,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":101,"speed":4}""")]
    [InlineData("""{"direction":"down","distance":1,"speed":0}""")]
    [InlineData("""{"direction":"down","distance":1,"speed":7}""")]
    [InlineData("""{"direction":"down","distance":1}""")]
    [InlineData("""{"direction":"up","distance":1,"speed":4,"wait":true}""")]
    [InlineData("""{"direction":"left","distance":1.5,"speed":4}""")]
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
        Assert.Equal("Faire défiler la carte", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ScrollMap));
        Assert.Equal("Direction", MapEventEditorLabels.Field("direction"));
        Assert.Equal("Distance", MapEventEditorLabels.Field("distance"));
        Assert.Equal("Vitesse", MapEventEditorLabels.Field("speed"));
        Assert.Equal("Bas", MapEventEditorLabels.ScrollDirection(MapEventScroll.Down));
        Assert.Equal("Haut", MapEventEditorLabels.ScrollDirection(MapEventScroll.Up));
        Assert.Equal("Gauche", MapEventEditorLabels.ScrollDirection(MapEventScroll.Left));
        Assert.Equal("Droite", MapEventEditorLabels.ScrollDirection(MapEventScroll.Right));
        Assert.Equal(
            "Faire défiler la carte : Droite · 2 tuiles · vitesse 4",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ScrollMap,
                """{"direction":"right","distance":2,"speed":4}"""));
        Assert.Equal(
            "Faire défiler la carte : Haut · 1 tuile · vitesse 6",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ScrollMap,
                """{"direction":"up","distance":1,"speed":6}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesScrollWithoutANewOpcode()
    {
        var right = MapEventScreenOp.ForScroll(MapEventScroll.Right, 2, 4);
        var up = MapEventScreenOp.ForScroll(MapEventScroll.Up, 1, 6);
        var tint = MapEventScreenOp.ForTint(12, 24, 48, 80, 500);
        var message = MapEventScreenWire.Compose(
            [
                MapEventVisualOp.ForScreen(right),
                MapEventVisualOp.ForScreen(up),
                MapEventVisualOp.ForScreen(tint),
            ],
            null,
            "Travelling.",
            "Nom (slug)");
        Assert.StartsWith("scroll:right:2:4\n", message, StringComparison.Ordinal);
        Assert.Contains("scroll:up:1:6\n", message, StringComparison.Ordinal);
        Assert.Contains("tint:12:24:48:80:500\n", message, StringComparison.Ordinal);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(message, out var ops, out var after));
        Assert.Equal(3, ops.Count);
        Assert.Equal(right, ops[0].Screen);
        Assert.Equal(up, ops[1].Screen);
        Assert.Equal(tint, ops[2].Screen);
        Assert.Equal("Travelling.", after);
        Assert.False(MapEventScreenWire.TryParseScreenLine("scroll:right:2", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("scroll:north:1:4", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("scroll:down:101:4", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("scroll:down:1:7", out _));

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Playback_ScrollComposesAndLeavesTint()
    {
        var tinted = new MapEventScreenFrame(10, 1, 2, 3, 40);
        var right = MapEventScreenOp.ForScroll(MapEventScroll.Right, 1, 4);
        Assert.Equal(267, right.DurationMs);
        Assert.Equal(0, MapEventScreenPlayback.Sample(right, tinted, 0).ScrollX);
        var halfway = MapEventScreenPlayback.Sample(right, tinted, 134);
        Assert.Equal(24, halfway.ScrollX);
        Assert.Equal(0, halfway.ScrollY);
        Assert.Equal(40, halfway.Opacity);
        Assert.Equal(10, halfway.Fade);
        var settled = MapEventScreenPlayback.EndState(right, tinted);
        Assert.Equal(48, settled.ScrollX);
        Assert.Equal(40, settled.Opacity);

        var up = MapEventScreenOp.ForScroll(MapEventScroll.Up, 2, 6);
        var composed = MapEventScreenPlayback.EndState(up, settled);
        Assert.Equal(48, composed.ScrollX);
        Assert.Equal(-96, composed.ScrollY);

        var faded = MapEventScreenPlayback.EndState(MapEventScreenOp.ForFadeOut(0), composed);
        Assert.Equal(MapEventScreen.MaxChannel, faded.Fade);
        Assert.Equal(48, faded.ScrollX);
        Assert.Equal(-96, faded.ScrollY);

        var shaken = MapEventScreenPlayback.EndState(MapEventScreenOp.ForShake(8, 5, 400), faded);
        Assert.Equal(0, shaken.ShakeX);
        Assert.Equal(48, shaken.ScrollX);
        Assert.Equal(0, MapEventScroll.DurationMs(0, 4));
        Assert.Equal((0, 0), MapEventScroll.DeltaTiles("nope", 3));
    }

    [Fact]
    public void Session_ScrollComposesAndClearsOnMapChange()
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "hero" };
        session.ApplyScreenOp(MapEventScreenOp.ForTint(8, 16, 32, 64, 1000));
        session.ApplyScreenOp(MapEventScreenOp.ForScroll(MapEventScroll.Right, 2, 4));
        session.ApplyScreenOp(MapEventScreenOp.ForScroll(MapEventScroll.Down, 1, 4));
        session.ApplyScreenOp(MapEventScreenOp.ForShake(8, 5, 400));
        Assert.Equal(2, session.ScrollTilesX);
        Assert.Equal(1, session.ScrollTilesY);
        Assert.Equal(new MapEventScreenTone(8, 16, 32, 64), session.ScreenTint);
        var mapId = session.CurrentMapId;
        session.CurrentMapId = mapId;
        Assert.Equal(2, session.ScrollTilesX);
        session.CurrentMapId = mapId + 1;
        Assert.Equal(0, session.ScrollTilesX);
        Assert.Equal(0, session.ScrollTilesY);
        Assert.Equal(64, session.ScreenTint.Opacity);
    }
}
