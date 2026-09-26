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

public sealed class ShowPictureCommandTests
{
    [Fact]
    public void Palette_AfficherEtEffacer_DefaultsAndFrenchLabels()
    {
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ShowPictureId && entry.Label == "Afficher image");
        Assert.Contains(
            MapEventCommandPalette.Entries,
            entry => entry.Id == MapEventCommandPalette.ErasePictureId && entry.Label == "Effacer image");

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ShowPictureId, out var show));
        Assert.Equal(MapEventCommandDiscriminators.ShowPicture, show.Discriminator);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(show, out var showError), showError);
        Assert.True(MapEventParameterSchemas.TryParseShowPicture(show.ParameterJson, out var picture, out var parseErr), parseErr);
        Assert.Equal(1, picture.PictureId);
        Assert.Equal(MapEventPicture.DefaultAsset, picture.Asset);
        Assert.Equal(0, picture.X);
        Assert.Equal(0, picture.Y);
        Assert.Equal(255, picture.Opacity);
        Assert.Equal(MapEventPicture.BlendNormal, picture.Blend);

        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.ErasePictureId, out var erase));
        Assert.Equal(MapEventCommandDiscriminators.ErasePicture, erase.Discriminator);
        Assert.True(MapEventParameterSchemas.TryParseErasePicture(erase.ParameterJson, out var id, out var eraseErr), eraseErr);
        Assert.Equal(1, id);

        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(32, (byte)PacketId.InteractResult);
    }

    [Theory]
    [InlineData("normal", "normal")]
    [InlineData("ADD", "add")]
    [InlineData("subtract", "subtract")]
    [InlineData("soustraction", "subtract")]
    public void Validator_AcceptsBlendAliases(string raw, string canonical)
    {
        var command = Show(rawBlend: raw);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
        Assert.True(MapEventParameterSchemas.TryParseShowPicture(command.ParameterJson, out var picture, out error), error);
        Assert.Equal(canonical, picture.Blend);
    }

    [Theory]
    [InlineData("""{"pictureId":0,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":101,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"asset":"../secret.png","x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"asset":"C:/art.png","x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"asset":"Assets/Pictures/note.txt","x":0,"y":0,"opacity":255,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":256,"blend":"normal"}""")]
    [InlineData("""{"pictureId":1,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"screen"}""")]
    [InlineData("""{"pictureId":1,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"normal","zoom":100}""")]
    [InlineData("""{"pictureId":"1"}""")]
    public void Validator_RejectsShowPicture(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowPicture,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Validator_RejectsEraseWithoutNumber()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ErasePicture,
            ParameterJson = """{"slot":1}""",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("pictureId", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Labels_ListTheSlotAndFile()
    {
        Assert.Equal("Afficher image", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ShowPicture));
        Assert.Equal("Effacer image", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.ErasePicture));
        Assert.Equal("Numéro", MapEventEditorLabels.Field("pictureId"));
        Assert.Equal("Opacité", MapEventEditorLabels.Field("opacity"));
        Assert.Equal("Synthèse", MapEventEditorLabels.Field("blend"));
        Assert.Equal("Addition", MapEventEditorLabels.PictureBlend(MapEventPicture.BlendAdd));
        Assert.Equal(
            "1. Afficher image 2 : placeholder.png (16, 32) · Addition",
            MapEventEditorLabels.CommandListLine(
                0,
                MapEventCommandDiscriminators.ShowPicture,
                """{"pictureId":2,"asset":"Assets/Pictures/placeholder.png","x":16,"y":32,"opacity":200,"blend":"add"}"""));
        Assert.Equal(
            "Effacer image 2",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.ErasePicture,
                """{"pictureId":2}"""));
    }

    [Fact]
    public void InteractMessage_PrefixesPicturesWithoutANewOpcode()
    {
        var shopId = Guid.Parse("aaaaaaaa-0005-4000-8000-000000000001");
        var show = MapEventPictureOp.ForShow(1, MapEventPicture.DefaultAsset, 0, 0, 255, MapEventPicture.BlendNormal);
        var erase = MapEventPictureOp.ForErase(1);
        var bareShop = MapEventShopOpen.FormatInteractMessage(shopId, "Bienvenue.");
        Assert.Equal(
            bareShop,
            MapEventPictureWire.Compose(Array.Empty<MapEventPictureOp>(), shopId, "Bienvenue.", "ignored"));
        Assert.Equal("Nom (slug)", MapEventPictureWire.Compose(null, null, null, "Nom (slug)"));

        var message = MapEventPictureWire.Compose(new[] { show, erase }, shopId, "Bienvenue.", "Nom (slug)");
        Assert.True(MapEventPictureWire.TryTakeInteractMessage(message, out var ops, out var afterPictures));
        Assert.Equal(2, ops.Count);
        Assert.Equal(show, ops[0]);
        Assert.True(ops[1].Erase);
        Assert.True(MapEventShopOpen.TryTakeInteractMessage(afterPictures, out var parsedShop, out var text));
        Assert.Equal(shopId, parsedShop);
        Assert.Equal("Bienvenue.", text);

        var body = Phase8Wire.BuildInteractResult(true, message, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        Assert.True(Phase8Wire.TryParseInteractResult(body, out var ok, out var parsed, out _));
        Assert.True(ok);
        Assert.Equal(message, parsed);
        Assert.True(Encoding.UTF8.GetByteCount(message) <= MapEventPictureWire.MaxInteractUtf8Bytes);
    }

    [Fact]
    public void Session_ShowReplacesSlot_EraseRemovesIt()
    {
        var session = new Session { Id = Guid.NewGuid(), Username = "hero" };
        session.ApplyPictureOp(MapEventPictureOp.ForShow(5, MapEventPicture.DefaultAsset, 4, 8, 128, MapEventPicture.BlendSubtract));
        session.ApplyPictureOp(MapEventPictureOp.ForShow(5, MapEventPicture.DefaultAsset, 9, 8, 255, MapEventPicture.BlendNormal));
        Assert.Equal(9, session.Pictures[5].X);
        Assert.Equal(MapEventPicture.BlendNormal, session.Pictures[5].Blend);
        session.ApplyPictureOp(MapEventPictureOp.ForErase(5));
        Assert.False(session.Pictures.ContainsKey(5));
        session.ApplyPictureOp(MapEventPictureOp.ForErase(5));
        Assert.Empty(session.Pictures);
    }

    [Fact]
    public void PictureOp_RoundTripsWithWebJson()
    {
        var op = MapEventPictureOp.ForShow(2, MapEventPicture.DefaultAsset, -4, 12, 200, MapEventPicture.BlendAdd);
        var json = JsonSerializer.Serialize(new[] { op }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var back = JsonSerializer.Deserialize<MapEventPictureOp[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(back);
        Assert.Equal(op, Assert.Single(back));
    }

    private static MapEventCommandDefinition Show(string rawBlend) =>
        new()
        {
            Discriminator = MapEventCommandDiscriminators.ShowPicture,
            ParameterJson = $$"""{"pictureId":1,"asset":"Assets/Pictures/placeholder.png","x":0,"y":0,"opacity":255,"blend":"{{rawBlend}}"}""",
        };
}
