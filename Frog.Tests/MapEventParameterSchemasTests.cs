using Frog.Core.Events;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventParameterSchemasTests
{
    [Fact]
    public void TryParseShowText_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseShowText("""{"text":"Bonjour"}""", out var text, out var err));
        Assert.Equal("Bonjour", text);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseSetSwitch_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseSetSwitch(
            """{"switchId":"door_open","value":true}""",
            out var id,
            out var value,
            out var err));
        Assert.Equal("door_open", id);
        Assert.True(value);
        Assert.Null(err);
    }

    [Fact]
    public void TryParseCharacterSwitchCondition_ValidJson()
    {
        Assert.True(MapEventParameterSchemas.TryParseCharacterSwitchCondition(
            """{"switchId":"quest_a","value":false}""",
            out var id,
            out var expected,
            out var err));
        Assert.Equal("quest_a", id);
        Assert.False(expected);
        Assert.Null(err);
    }
}
