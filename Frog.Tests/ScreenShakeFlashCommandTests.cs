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

public sealed class ScreenShakeFlashCommandTests
{
    [Fact]
    public void Palette_TremblementEtFlash_DefaultsAndFrenchLabels()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ShakeScreenId && entry.Label == "Tremblement écran");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.FlashScreenId && entry.Label == "Flash écran");

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ShakeScreenId, out var shake));
        Assert.Equal(MapEventCommandDiscriminators.ShakeScreen, shake.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(shake, out var shakeError), shakeError);
        Assert.True(MapEventParameterSchemas.TryParseShakeScreen(shake.ParameterJson, out var parsedShake, out var shakeErr), shakeErr);
        Assert.True(parsedShake.IsShake);
        Assert.Equal(MapEventScreen.DefaultPower, parsedShake.Power);
        Assert.Equal(MapEventScreen.DefaultSpeed, parsedShake.Speed);
        Assert.Equal(MapEventScreen.DefaultDurationMs, parsedShake.DurationMs);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.FlashScreenId, out var flash));
        Assert.Equal(MapEventCommandDiscriminators.FlashScreen, flash.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(flash, out var flashError), flashError);
        Assert.True(MapEventParameterSchemas.TryParseFlashScreen(flash.ParameterJson, out var parsedFlash, out var flashErr), flashErr);
        Assert.True(parsedFlash.IsFlash);
        Assert.Equal(MapEventScreen.DefaultFlashRed, parsedFlash.Red);
        Assert.Equal(MapEventScreen.DefaultFlashGreen, parsedFlash.Green);
        Assert.Equal(MapEventScreen.DefaultFlashBlue, parsedFlash.Blue);
        Assert.Equal(MapEventScreen.DefaultFlashOpacity, parsedFlash.Opacity);
        Assert.Equal(MapEventScreen.DefaultDurationMs, parsedFlash.DurationMs);

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal(TileAssetMetrics.TargetTileSizePixels, MapEventScreen.MaxPower);
    }

    [Theory]
    [InlineData("""{"power":-1,"speed":5,"durationMs":100}""")]
    [InlineData("""{"power":49,"speed":5,"durationMs":100}""")]
    [InlineData("""{"power":8,"speed":0,"durationMs":100}""")]
    [InlineData("""{"power":8,"speed":10,"durationMs":100}""")]
    [InlineData("""{"power":8,"speed":5}""")]
    [InlineData("""{"power":8,"speed":5,"durationMs":100,"wait":true}""")]
    [InlineData("""{"power":1.5,"speed":5,"durationMs":100}""")]
    public void Validator_RejectsShake(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShakeScreen,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("""{"red":256,"green":0,"blue":0,"opacity":128,"durationMs":0}""")]
    [InlineData("""{"red":255,"green":255,"blue":255,"opacity":170}""")]
    [InlineData("""{"red":255,"green":255,"blue":255,"opacity":170,"durationMs":0,"wait":true}""")]
    public void Validator_RejectsFlash(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.FlashScreen,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_NameThePowerAndTheFlashColor()
    {
        Assert.Equal("Tremblement écran", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ShakeScreen));
        Assert.Equal("Flash écran", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.FlashScreen));
        Assert.Equal("Puissance", MapEventEditorLabels.Field("power"));
        Assert.Equal("Vitesse", MapEventEditorLabels.Field("speed"));
        Assert.Equal(
            "Tremblement écran : puissance 8 · vitesse 5 (1000 ms)",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ShakeScreen,
                """{"power":8,"speed":5,"durationMs":1000}"""));
        Assert.Equal(
            "Flash écran : R255 V255 B255 · op. 170 (200 ms)",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.FlashScreen,
                """{"red":255,"green":255,"blue":255,"opacity":170,"durationMs":200}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesShakeAndFlashWithoutANewOpcode()
    {
        var shake = MapEventScreenOp.ForShake(8, 1, 1000);
        var flash = MapEventScreenOp.ForFlash(255, 255, 255, 170, 200);
        var tint = MapEventScreenOp.ForTint(12, 24, 48, 80, 500);
        var visuals = new[]
        {
            MapEventVisualOp.ForScreen(shake),
            MapEventVisualOp.ForScreen(flash),
            MapEventVisualOp.ForScreen(tint),
        };

        var message = MapEventScreenWire.Compose(visuals, null, "Secousse.", "Nom (slug)");
        Assert.StartsWith("shake:8:1:1000\n", message, StringComparison.Ordinal);
        Assert.Contains("flash:255:255:255:170:200\n", message, StringComparison.Ordinal);
        Assert.Contains("tint:12:24:48:80:500\n", message, StringComparison.Ordinal);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(message, out var ops, out var after));
        Assert.Equal(3, ops.Count);
        Assert.Equal(shake, ops[0].Screen);
        Assert.Equal(flash, ops[1].Screen);
        Assert.Equal(tint, ops[2].Screen);
        Assert.Equal("Secousse.", after);
        Assert.False(MapEventScreenWire.TryParseScreenLine("shake:8:1", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("shake:8:0:100", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("flash:1:2:3:4", out _));

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Playback_ShakeReturnsToRest_AndFlashFadesWithoutReplacingTint()
    {
        var tinted = new MapEventScreenFrame(0, 10, 20, 30, 40);
        var shake = MapEventScreenOp.ForShake(8, 1, 1000);
        Assert.Equal(0, MapEventScreenPlayback.Sample(shake, tinted, 0).ShakeX);
        Assert.Equal(8, MapEventScreenPlayback.ShakeOffset(8, 1, 250));
        var peak = MapEventScreenPlayback.Sample(shake, tinted, 250);
        Assert.Equal(8, peak.ShakeX);
        Assert.Equal(40, peak.Opacity);
        Assert.Equal(10, peak.Red);
        Assert.Equal(0, peak.FlashOpacity);
        var rested = MapEventScreenPlayback.EndState(shake, peak);
        Assert.Equal(0, rested.ShakeX);
        Assert.Equal(new MapEventScreenTone(10, 20, 30, 40), new MapEventScreenTone(rested.Red, rested.Green, rested.Blue, rested.Opacity));

        var flash = MapEventScreenOp.ForFlash(255, 255, 255, 200, 100);
        var start = MapEventScreenPlayback.Sample(flash, tinted, 0);
        Assert.Equal(200, start.FlashOpacity);
        Assert.Equal(255, start.FlashRed);
        Assert.Equal(40, start.Opacity);
        var halfway = MapEventScreenPlayback.Sample(flash, tinted, 50);
        Assert.Equal(100, halfway.FlashOpacity);
        Assert.Equal(40, halfway.Opacity);
        var done = MapEventScreenPlayback.EndState(flash, start);
        Assert.Equal(0, done.FlashOpacity);
        Assert.Equal(40, done.Opacity);
        Assert.Equal(0, MapEventScreenPlayback.Sample(MapEventScreenOp.ForFlash(255, 0, 0, 255, 0), tinted, 0).FlashOpacity);
    }

    [Fact]
    public void Session_ShakeAndFlashDoNotClearFadeOrTint()
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "hero" };
        session.ApplyScreenOp(MapEventScreenOp.ForTint(8, 16, 32, 64, 1000));
        session.ApplyScreenOp(MapEventScreenOp.ForFadeOut(500));
        session.ApplyScreenOp(MapEventScreenOp.ForShake(8, 5, 400));
        session.ApplyScreenOp(MapEventScreenOp.ForFlash(255, 255, 255, 170, 200));
        Assert.Equal(MapEventScreen.MaxChannel, session.ScreenFade);
        Assert.Equal(new MapEventScreenTone(8, 16, 32, 64), session.ScreenTint);
    }
}
