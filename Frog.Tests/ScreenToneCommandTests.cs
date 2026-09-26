using System;
using System.Text;
using Frog.Application.Events;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class ScreenToneCommandTests
{
    [Fact]
    public void Palette_FonduEtTeinte_DefaultsAndFrenchLabels()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.FadeOutScreenId && entry.Label == "Fondu en fermeture");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.FadeInScreenId && entry.Label == "Fondu en ouverture");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.TintScreenId && entry.Label == "Teinte écran");

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.FadeOutScreenId, out var fadeOut));
        Assert.Equal(MapEventCommandDiscriminators.FadeOutScreen, fadeOut.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(fadeOut, out var fadeOutError), fadeOutError);
        Assert.True(
            MapEventParameterSchemas.TryParseFadeScreen(fadeOut.ParameterJson, fadeOut: true, out var parsedOut, out var outErr),
            outErr);
        Assert.True(parsedOut.IsFadeOut);
        Assert.Equal(MapEventScreen.DefaultDurationMs, parsedOut.DurationMs);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.FadeInScreenId, out var fadeIn));
        Assert.Equal(MapEventCommandDiscriminators.FadeInScreen, fadeIn.Discriminator);
        Assert.True(
            MapEventParameterSchemas.TryParseFadeScreen(fadeIn.ParameterJson, fadeOut: false, out var parsedIn, out var inErr),
            inErr);
        Assert.True(parsedIn.IsFadeIn);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.TintScreenId, out var tint));
        Assert.Equal(MapEventCommandDiscriminators.TintScreen, tint.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(tint, out var tintError), tintError);
        Assert.True(MapEventParameterSchemas.TryParseTintScreen(tint.ParameterJson, out var parsedTint, out var tintErr), tintErr);
        Assert.Equal(MapEventScreen.DefaultTintRed, parsedTint.Red);
        Assert.Equal(MapEventScreen.DefaultTintGreen, parsedTint.Green);
        Assert.Equal(MapEventScreen.DefaultTintBlue, parsedTint.Blue);
        Assert.Equal(MapEventScreen.DefaultTintOpacity, parsedTint.Opacity);
        Assert.Equal(MapEventScreen.DefaultDurationMs, parsedTint.DurationMs);

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
    }

    [Theory]
    [InlineData("""{"durationMs":-1}""")]
    [InlineData("""{"durationMs":60001}""")]
    [InlineData("""{"milliseconds":1000}""")]
    [InlineData("""{"durationMs":1000,"wait":true}""")]
    [InlineData("""{"durationMs":1.5}""")]
    public void Validator_RejectsFade(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.FadeOutScreen,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("""{"red":256,"green":0,"blue":0,"opacity":128,"durationMs":0}""")]
    [InlineData("""{"red":0,"green":0,"blue":0,"opacity":-1,"durationMs":0}""")]
    [InlineData("""{"red":0,"green":0,"blue":0,"opacity":128}""")]
    [InlineData("""{"red":0,"green":0,"blue":0,"opacity":128,"durationMs":0,"gray":0}""")]
    public void Validator_RejectsTint(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.TintScreen,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_NameTheDurationAndColor()
    {
        Assert.Equal("Fondu en fermeture", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.FadeOutScreen));
        Assert.Equal("Fondu en ouverture", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.FadeInScreen));
        Assert.Equal("Teinte écran", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.TintScreen));
        Assert.Equal("Durée (ms)", MapEventEditorLabels.Field("durationMs"));
        Assert.Equal("Rouge", MapEventEditorLabels.Field("red"));
        Assert.Equal("Vert", MapEventEditorLabels.Field("green"));
        Assert.Equal("Bleu", MapEventEditorLabels.Field("blue"));
        Assert.Equal(
            "Fondu en fermeture : 800 ms",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.FadeOutScreen,
                """{"durationMs":800}"""));
        Assert.Equal(
            "Fondu en ouverture : 0 ms",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.FadeInScreen,
                """{"durationMs":0}"""));
        Assert.Equal(
            "Teinte écran : R10 V20 B30 · op. 90 (400 ms)",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.TintScreen,
                """{"red":10,"green":20,"blue":30,"opacity":90,"durationMs":400}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesFadeAndTintWithoutANewOpcode()
    {
        var shopId = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");
        var fade = MapEventScreenOp.ForFadeOut(1000);
        var tint = MapEventScreenOp.ForTint(12, 24, 48, 80, 500);
        var picture = MapEventPictureOp.ForShow(1, MapEventPicture.DefaultAsset, 0, 0, 255, MapEventPicture.BlendNormal);
        var visuals = new[]
        {
            MapEventVisualOp.ForScreen(fade),
            MapEventVisualOp.ForPicture(picture),
            MapEventVisualOp.ForScreen(tint),
        };

        var picturesOnly = new[] { MapEventVisualOp.ForPicture(picture) };
        Assert.Equal(
            MapEventPictureWire.Compose(new[] { picture }, shopId, "Bienvenue.", "ignored"),
            MapEventScreenWire.Compose(picturesOnly, shopId, "Bienvenue.", "ignored"));
        Assert.Equal(
            "Nom (slug)",
            MapEventScreenWire.Compose(Array.Empty<MapEventVisualOp>(), null, null, "Nom (slug)"));

        var message = MapEventScreenWire.Compose(visuals, shopId, "Bienvenue.", "Nom (slug)");
        Assert.StartsWith("fade:out:1000\n", message, StringComparison.Ordinal);
        Assert.Contains("tint:12:24:48:80:500\n", message, StringComparison.Ordinal);
        Assert.True(MapEventScreenWire.TryTakeInteractMessage(message, out var ops, out var after));
        Assert.Equal(3, ops.Count);
        Assert.Equal(fade, ops[0].Screen);
        Assert.Equal(picture, ops[1].Picture);
        Assert.Equal(tint, ops[2].Screen);
        Assert.True(MapEventShopOpen.TryTakeInteractMessage(after, out var parsedShop, out var text));
        Assert.Equal(shopId, parsedShop);
        Assert.Equal("Bienvenue.", text);
        Assert.False(MapEventScreenWire.TryParseScreenLine("fade:side:10", out _));
        Assert.False(MapEventScreenWire.TryParseScreenLine("tint:1:2:3:4", out _));

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Playback_SamplesTowardTheSettledFrame()
    {
        var from = MapEventScreenFrame.Clear;
        var fade = MapEventScreenOp.ForFadeOut(200);
        Assert.Equal(from, MapEventScreenPlayback.Sample(fade, from, 0));
        Assert.Equal(128, MapEventScreenPlayback.Sample(fade, from, 100).Fade);
        Assert.Equal(MapEventScreen.MaxChannel, MapEventScreenPlayback.Sample(fade, from, 200).Fade);
        Assert.Equal(0, MapEventScreenPlayback.Sample(fade, from, 100).Opacity);

        var tint = MapEventScreenOp.ForTint(0, 0, 100, 200, 100);
        var halfway = MapEventScreenPlayback.Sample(tint, from, 50);
        Assert.Equal(0, halfway.Fade);
        Assert.Equal(50, halfway.Blue);
        Assert.Equal(100, halfway.Opacity);
        var end = MapEventScreenPlayback.EndState(tint, from);
        Assert.Equal(new MapEventScreenFrame(0, 0, 0, 100, 200), end);

        var opened = MapEventScreenPlayback.EndState(MapEventScreenOp.ForFadeIn(0), end with { Fade = 255 });
        Assert.Equal(0, opened.Fade);
        Assert.Equal(200, opened.Opacity);
    }

    [Fact]
    public void Session_FadeDoesNotClearTint_AndTintDoesNotClearFade()
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "hero" };
        session.ApplyScreenOp(MapEventScreenOp.ForTint(8, 16, 32, 64, 1000));
        session.ApplyScreenOp(MapEventScreenOp.ForFadeOut(500));
        Assert.Equal(MapEventScreen.MaxChannel, session.ScreenFade);
        Assert.Equal(new MapEventScreenTone(8, 16, 32, 64), session.ScreenTint);
        session.ApplyScreenOp(MapEventScreenOp.ForFadeIn(500));
        Assert.Equal(0, session.ScreenFade);
        Assert.Equal(64, session.ScreenTint.Opacity);
        session.ApplyScreenOp(MapEventScreenOp.ForTint(0, 0, 0, 0, 0));
        Assert.Equal(MapEventScreenTone.Clear, session.ScreenTint);
    }

    [Fact]
    public void Snapshot_KeepsPictureAndScreenOrder()
    {
        var snap = new MapEventExecutionSnapshot();
        snap.RecordScreen(MapEventScreenOp.ForFadeOut(10));
        snap.RecordPicture(MapEventPictureOp.ForShow(1, MapEventPicture.DefaultAsset, 0, 0, 255, MapEventPicture.BlendNormal));
        snap.RecordScreen(MapEventScreenOp.ForFadeIn(20));
        var visuals = snap.ExpandVisuals();
        Assert.Equal(3, visuals.Count);
        Assert.True(visuals[0].Screen?.IsFadeOut);
        Assert.False(visuals[1].Picture?.Erase);
        Assert.True(visuals[2].Screen?.IsFadeIn);

        var legacy = new MapEventExecutionSnapshot
        {
            PictureOps = [MapEventPictureOp.ForErase(4)],
        };
        var legacyVisuals = legacy.ExpandVisuals();
        Assert.True(Assert.Single(legacyVisuals).Picture?.Erase);
    }
}
