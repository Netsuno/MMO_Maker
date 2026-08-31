using System;
using System.Linq;
using Frog.Core.Events;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventCommandParameterValidatorTests
{
    [Fact]
    public void ValidateParameters_RejectsUnsupportedSchemaVersion()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 2,
            ParameterJson = """{"text":"Hi"}""",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("SchemaVersion", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_RejectsUnknownJsonProperty()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            ParameterJson = """{"text":"Hi","extra":true}""",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out var error));
        Assert.Contains("inconnue", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateParameters_AcceptsGiveItemWithOnceKey()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.GiveItem,
            ParameterJson = $$"""{"itemId":"{{Guid.NewGuid():D}}","quantity":1,"onceKey":"chest-a"}""",
        };
        Assert.True(MapEventCommandParameterValidator.ValidateParameters(command, out _));
    }

    [Fact]
    public void ValidateParameters_RejectsUnknownDiscriminator()
    {
        var command = new MapEventCommandDefinition
        {
            Discriminator = "not_a_real_command",
            ParameterJson = "{}",
        };
        Assert.False(MapEventCommandParameterValidator.ValidateParameters(command, out _));
    }
}
