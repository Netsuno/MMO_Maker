using System;
using System.Text;
using System.Text.Json;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MoveTintPictureCommandTests
{
    [Fact]
    public void Palette_DeplacerEtTeinter_DefaultsAndFrenchLabels()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.MovePictureId && entry.Label == "Déplacer image");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.TintPictureId && entry.Label == "Teinter image");

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.MovePictureId, out var move));
        Assert.Equal(MapEventCommandDiscriminators.MovePicture, move.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(move, out var moveError), moveError);
        Assert.True(MapEventParameterSchemas.TryParseMovePicture(move.ParameterJson, out var moved, out var parseErr), parseErr);
        Assert.True(moved.IsMove);
        Assert.Equal(1, moved.PictureId);
        Assert.Equal(0, moved.X);
        Assert.Equal(0, moved.Y);
        Assert.Equal(255, moved.Opacity);
        Assert.Equal(MapEventPicture.BlendNormal, moved.Blend);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.TintPictureId, out var tint));
        Assert.Equal(MapEventCommandDiscriminators.TintPicture, tint.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(tint, out var tintError), tintError);
        Assert.True(MapEventParameterSchemas.TryParseTintPicture(tint.ParameterJson, out var tinted, out var tintErr), tintErr);
        Assert.True(tinted.IsTint);
        Assert.Equal(1, tinted.PictureId);
        Assert.Equal(MapEventScreen.DefaultTintRed, tinted.Red);
        Assert.Equal(MapEventScreen.DefaultTintGreen, tinted.Green);
        Assert.Equal(MapEventScreen.DefaultTintBlue, tinted.Blue);
        Assert.Equal(MapEventScreen.DefaultTintOpacity, tinted.TintOpacity);

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
    }

    [Theory]
    [InlineData("""{"pictureId":0,"x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"x":0,"y":0,"opacity":256,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"x":0,"y":0,"opacity":255,"blend":"screen"}""")]
    [InlineData("""{"pictureId":1,"x":0,"y":0,"opacity":255,"blend":"normal","asset":"Assets/Pictures/placeholder.png"}""")]
    [InlineData("""{"pictureId":1,"x":0,"y":0,"opacity":255}""")]
    public void Validator_RejectsMovePicture(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.MovePicture,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("""{"pictureId":1,"red":256,"green":0,"blue":0,"opacity":128}""")]
    [InlineData("""{"pictureId":1,"red":0,"green":0,"blue":0}""")]
    [InlineData("""{"pictureId":1,"red":0,"green":0,"blue":0,"opacity":128,"durationMs":0}""")]
    public void Validator_RejectsTintPicture(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.TintPicture,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_DescribeTheSlot()
    {
        Assert.Equal("Déplacer image", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.MovePicture));
        Assert.Equal("Teinter image", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.TintPicture));
        Assert.Equal(
            "Déplacer image 2 (16, 32) · Addition",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.MovePicture,
                """{"pictureId":2,"x":16,"y":32,"opacity":200,"blend":"add"}"""));
        Assert.Equal(
            "Teinter image 2 : R255 V10 B20 · op. 180",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.TintPicture,
                """{"pictureId":2,"red":255,"green":10,"blue":20,"opacity":180}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesMoveAndTintWithoutANewOpcode()
    {
        var show = MapEventPictureOp.ForShow(1, MapEventPicture.DefaultAsset, 0, 0, 255, MapEventPicture.BlendNormal);
        var move = MapEventPictureOp.ForMove(1, -4, 12, 200, MapEventPicture.BlendAdd);
        var tint = MapEventPictureOp.ForTint(1, 255, 0, 0, 128);
        var message = MapEventPictureWire.Compose(new[] { show, move, tint }, null, "Bouge.", "ignored");
        Assert.True(MapEventPictureWire.TryTakeInteractMessage(message, out var ops, out var rest));
        Assert.Equal("Bouge.", rest);
        Assert.Equal(show, ops[0]);
        Assert.Equal(move, ops[1]);
        Assert.Equal(tint, ops[2]);
        Assert.Contains("pic:move:1:-4:12:200:add", message, StringComparison.Ordinal);
        Assert.Contains("pic:tint:1:255:0:0:128", message, StringComparison.Ordinal);

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Session_MoveKeepsAssetAndTint_ShowResetsTint_MissingSlotIsIgnored()
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "hero" };
        session.ApplyPictureOp(MapEventPictureOp.ForMove(3, 1, 1, 255, MapEventPicture.BlendNormal));
        session.ApplyPictureOp(MapEventPictureOp.ForTint(3, 1, 2, 3, 4));
        Assert.Empty(session.Pictures);

        session.ApplyPictureOp(MapEventPictureOp.ForShow(3, MapEventPicture.DefaultAsset, 4, 8, 128, MapEventPicture.BlendSubtract));
        session.ApplyPictureOp(MapEventPictureOp.ForTint(3, 9, 8, 7, 40));
        session.ApplyPictureOp(MapEventPictureOp.ForMove(3, 9, 1, 255, MapEventPicture.BlendAdd));
        var shown = session.Pictures[3];
        Assert.Equal(MapEventPicture.DefaultAsset, shown.Asset);
        Assert.Equal(9, shown.X);
        Assert.Equal(1, shown.Y);
        Assert.Equal(MapEventPicture.BlendAdd, shown.Blend);
        Assert.Equal(9, shown.TintRed);
        Assert.Equal(40, shown.TintOpacity);

        session.ApplyPictureOp(MapEventPictureOp.ForShow(3, MapEventPicture.DefaultAsset, 0, 0, 255, MapEventPicture.BlendNormal));
        Assert.Equal(0, session.Pictures[3].TintOpacity);
        Assert.Equal(0, session.Pictures[3].X);
    }

    [Fact]
    public void PictureOp_MoveAndTintRoundTripWithWebJson()
    {
        var ops = new[]
        {
            MapEventPictureOp.ForMove(2, -4, 12, 200, MapEventPicture.BlendAdd),
            MapEventPictureOp.ForTint(2, 1, 2, 3, 40),
        };
        var json = JsonSerializer.Serialize(ops, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var back = JsonSerializer.Deserialize<MapEventPictureOp[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(back);
        Assert.Equal(ops, back);
    }
}
