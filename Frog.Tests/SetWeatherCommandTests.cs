using System;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Core.Weather;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class SetWeatherCommandTests
{
    [Fact]
    public void Palette_ChangerMeteo_DefaultsToClearAndValidates()
    {
        Assert.Contains(MapEventCommandPalette.Entries, entry => entry.Id == MapEventCommandPalette.SetWeatherId && entry.Label == "Changer météo");
        Assert.True(MapEventCommandPalette.TryCreate(MapEventCommandPalette.SetWeatherId, out var command));
        Assert.Equal(MapEventCommandDiscriminators.SetWeather, command.Discriminator);
        Assert.True(command.Validate(out var shape), shape);
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
        Assert.True(MapEventParameterSchemas.TryParseSetWeather(command.ParameterJson, out var kind, out var parseErr), parseErr);
        Assert.Equal(WeatherKindId.Clear, kind);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(74, (byte)PacketId.EnvironmentStatePush);
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("rain")]
    [InlineData("fog")]
    [InlineData("RAIN")]
    public void Validator_AcceptsKnownKinds(string kind)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.SetWeather,
            ParameterJson = $$"""{"weatherKind":"{{kind}}"}""",
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out var error), error);
    }

    [Theory]
    [InlineData("""{"weatherKind":"snow"}""")]
    [InlineData("""{"weatherKind":"storm"}""")]
    [InlineData("""{"weatherKind":"pluie"}""")]
    [InlineData("""{"weatherKind":""}""")]
    [InlineData("""{"kind":"rain"}""")]
    [InlineData("""{"weatherKind":"rain","extra":1}""")]
    public void Validator_RejectsUnknownKindAndExtraFields(string parameterJson)
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.SetWeather,
            ParameterJson = parameterJson,
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Labels_ShowTheKindInTheCommandList()
    {
        Assert.Equal("Changer météo", MapEventEditorLabels.CommandKind(MapEventCommandDiscriminators.SetWeather));
        Assert.Equal("Météo", MapEventEditorLabels.Field("weatherKind"));
        Assert.Equal(
            "1. Changer météo : Pluie",
            MapEventEditorLabels.CommandListLine(
                0,
                MapEventCommandDiscriminators.SetWeather,
                """{"weatherKind":"rain"}"""));
        Assert.Equal(
            "Changer météo : Brouillard",
            MapEventEditorLabels.CommandSummary(
                MapEventCommandDiscriminators.SetWeather,
                """{"weatherKind":"fog"}"""));
        Assert.Equal("Clair", MapEventEditorLabels.WeatherKind(WeatherKindId.Clear));
    }

    [Fact]
    public void Resolver_SessionOverrideWinsOverPublishedKind_UntilClearedByMapChange()
    {
        var regionId = Guid.Parse("cccccccc-000c-4000-8000-000000000001");
        var profileId = WeatherCatalog.Phase8RainWeatherId;
        var published = new WeatherSnapshot
        {
            WeatherKind = WeatherKindId.Rain,
            LightingFactor = 0.55f,
            RegionId = regionId,
            WeatherProfileId = profileId,
        };

        var fog = WeatherResolver.ApplySessionOverride(published, WeatherKindId.Fog);
        Assert.Equal(WeatherKindId.Fog, fog.WeatherKind);
        Assert.Equal(published.LightingFactor, fog.LightingFactor);
        Assert.Equal(regionId, fog.RegionId);
        Assert.Equal(profileId, fog.WeatherProfileId);

        var ignored = WeatherResolver.ApplySessionOverride(published, "snow");
        Assert.Same(published, ignored);

        var body = Phase8Wire.BuildEnvironmentState(2, regionId, profileId, 140, fog.WeatherKind);
        Assert.True(Phase8Wire.TryParseEnvironmentState(body, out var mapId, out _, out _, out _, out var wireKind));
        Assert.Equal(2, mapId);
        Assert.Equal(WeatherKindId.Fog, wireKind);
        Assert.Equal(Phase8Wire.EnvironmentStateCoreBytes, 38);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            Username = "hero",
            CurrentMapId = 2,
            WeatherKindOverride = WeatherKindId.Fog,
        };
        Assert.Equal(WeatherKindId.Fog, session.WeatherKindOverride);
        session.CurrentMapId = 2;
        Assert.Equal(WeatherKindId.Fog, session.WeatherKindOverride);

        session.CurrentMapId = 3;
        Assert.Null(session.WeatherKindOverride);
        Assert.True(session.WeatherOverrideDroppedByMapChange);
        session.AcknowledgeWeatherOverrideDrop();
        Assert.False(session.WeatherOverrideDroppedByMapChange);
    }
}
