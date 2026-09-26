using System;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class ShowAnimationCommandTests
{
    [Fact]
    public void Palette_AfficherAnimation_DefaultsEventTargetAndFrenchLabel()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ShowAnimationId && entry.Label == "Afficher animation");
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ShowAnimationId, out var command));
        Assert.Equal(MapEventCommandDiscriminators.ShowAnimation, command.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
        Assert.True(MapEventParameterSchemas.TryParseShowAnimation(command.ParameterJson, out var parsed, out var parseErr), parseErr);
        Assert.Equal(MapEventAnimation.DefaultId, parsed.AnimationId);
        Assert.Equal(MapEventAnimation.TargetEvent, parsed.Target);
        Assert.Equal(MapEventScreen.DefaultDurationMs, parsed.DurationMs);
        Assert.False(parsed.IsPlaced);
        Assert.Equal(MapEventEffectCommitKind.SessionSide, MapEventEffectClassifier.Classify(command.Discriminator));
        Assert.True(Frog.Server.Gameplay.MapEventExecutionPlanner.CanExecuteTransactionally([command]));
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(255, MapEventAnimation.MaxTile);
    }

    [Theory]
    [InlineData("""{"animationId":0,"target":"event","durationMs":100}""")]
    [InlineData("""{"animationId":4,"target":"player","durationMs":100}""")]
    [InlineData("""{"animationId":1,"target":"npc","durationMs":100}""")]
    [InlineData("""{"animationId":1,"target":"event"}""")]
    [InlineData("""{"animationId":1,"target":"event","durationMs":100,"wait":true}""")]
    [InlineData("""{"animationId":1.5,"target":"event","durationMs":100}""")]
    public void Validator_RejectsShowAnimation(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowAnimation,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_NameTheAnimationAndTheTarget()
    {
        Assert.Equal("Afficher animation", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ShowAnimation));
        Assert.Equal("Animation", MapEventEditorLabels.Field("animationId"));
        Assert.Equal("Cible", MapEventEditorLabels.Field("target"));
        Assert.Equal("Étincelle", MapEventEditorLabels.AnimationName("1"));
        Assert.Equal("Soin", MapEventEditorLabels.AnimationName("2"));
        Assert.Equal("Impact", MapEventEditorLabels.AnimationName("3"));
        Assert.Equal("Joueur", MapEventEditorLabels.AnimationTarget(MapEventAnimation.TargetPlayer));
        Assert.Equal("Événement", MapEventEditorLabels.AnimationTarget(MapEventAnimation.TargetEvent));
        Assert.Equal(
            "Afficher animation : Étincelle sur Événement (600 ms)",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ShowAnimation,
                """{"animationId":1,"target":"event","durationMs":600}"""));
        Assert.Equal(
            "Afficher animation : Impact sur Joueur (400 ms)",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ShowAnimation,
                """{"animationId":3,"target":"player","durationMs":400}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesAnimationWithoutANewOpcode()
    {
        var onEvent = MapEventAnimationOp.Create(MapEventAnimation.SparkId, MapEventAnimation.TargetEvent, 600).At(4, 5);
        var onPlayer = MapEventAnimationOp.Create(MapEventAnimation.HitId, MapEventAnimation.TargetPlayer, 400).At(2, 3);
        var flash = MapEventScreenOp.ForFlash(255, 255, 255, 170, 200);
        var visuals = new[]
        {
            MapEventVisualOp.ForAnimation(onEvent),
            MapEventVisualOp.ForScreen(flash),
            MapEventVisualOp.ForAnimation(onPlayer),
        };

        var message = MapEventScreenWire.Compose(visuals, null, "Étincelle.", "Nom (slug)");
        Assert.StartsWith("anim:1:event:4:5:600\n", message, StringComparison.Ordinal);
        Assert.Contains("flash:255:255:255:170:200\n", message, StringComparison.Ordinal);
        Assert.Contains("anim:3:player:2:3:400\n", message, StringComparison.Ordinal);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(message, out var ops, out var after));
        Assert.Equal(3, ops.Count);
        Assert.Equal(onEvent, ops[0].Animation);
        Assert.Equal(flash, ops[1].Screen);
        Assert.Equal(onPlayer, ops[2].Animation);
        Assert.Equal("Étincelle.", after);
        Assert.False(MapEventAnimation.TryParseLine("anim:1:event:4:5", out _));
        Assert.False(MapEventAnimation.TryParseLine("anim:1:npc:0:0:100", out _));
        Assert.False(MapEventAnimation.TryParseLine("anim:1:event:-1:0:100", out _));
        Assert.False(MapEventAnimation.TryParseLine("anim:9:player:0:0:100", out _));

        var unresolved = MapEventAnimationOp.Create(1, MapEventAnimation.TargetEvent, 600);
        var dropped = MapEventScreenWire.Compose(
            [MapEventVisualOp.ForAnimation(unresolved)],
            null,
            "Texte.",
            null);
        Assert.Equal("Texte.", dropped);

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Anchor_UsesPlayerTileOrEventTile_AndPlaybackFadesOnTheTile()
    {
        var ops = new System.Collections.Generic.List<MapEventAnimationOp>
        {
            MapEventAnimationOp.Create(MapEventAnimation.HealId, MapEventAnimation.TargetEvent, 100),
            MapEventAnimationOp.Create(MapEventAnimation.SparkId, MapEventAnimation.TargetPlayer, 100),
        };
        MapEventAnimationAnchor.Resolve(ops, playerTileX: 2, playerTileY: 9, eventTileX: 6, eventTileY: 1);
        Assert.Equal(6, ops[0].TileX);
        Assert.Equal(1, ops[0].TileY);
        Assert.Equal(2, ops[1].TileX);
        Assert.Equal(9, ops[1].TileY);

        var clamped = MapEventAnimationOp.Create(1, MapEventAnimation.TargetPlayer, 100).At(-4, 999);
        Assert.Equal(0, clamped.TileX);
        Assert.Equal(MapEventAnimation.MaxTile, clamped.TileY);

        var tile = TileAssetMetrics.TargetTileSizePixels;
        var start = MapEventAnimationPlayback.Sample(ops[0], 0, tile);
        Assert.True(start.Visible);
        Assert.Equal(255, start.Opacity);
        Assert.Equal(40, start.Red);
        Assert.Equal(220, start.Green);
        Assert.Equal(Math.Max(1, tile / 4), start.RadiusPx);
        var halfway = MapEventAnimationPlayback.Sample(ops[0], 50, tile);
        Assert.Equal(127, halfway.Opacity);
        Assert.Equal(MapEventAnimationFrame.Hidden, MapEventAnimationPlayback.Sample(ops[0], 100, tile));
        Assert.Equal(MapEventAnimationFrame.Hidden, MapEventAnimationPlayback.Sample(ops[0], 0, 0));

        var sandbox = new MapEventTransactionalCommitSandbox();
        var identity = new MapEventExecutionIdentity(Guid.NewGuid(), Guid.NewGuid(), 1, 83);
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowAnimation,
            ParameterJson = """{"animationId":2,"target":"player","durationMs":480}""",
        };
        var unit = MapEventExecutionPlan.Ok(identity, [command]).AsTransactionalUnit();
        Assert.True(unit.IsSuccess, unit.Error);
        Assert.Equal(MapEventCommitDisposition.Committed, sandbox.TryCommit(unit).Disposition);
        Assert.Equal(0, sandbox.World.ScreenFade);
    }
}
