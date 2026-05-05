using Frog.Core.IO;
using Frog.Core.Maps;
using Xunit;

namespace Frog.Tests;

public sealed class MapSamplesTests
{
    [Fact]
    public void StarterMeadow_serializes_and_deserializes()
    {
        var serializer = new MapSerializer();
        var map = MapSamples.StarterMeadow(warpTargetMapId: 1);
        var bytes = serializer.Serialize(map);
        var roundtrip = serializer.Deserialize(bytes);
        Assert.Equal("Starter Meadow", roundtrip.Name);
        Assert.Equal(20, roundtrip.Width);
    }
}
