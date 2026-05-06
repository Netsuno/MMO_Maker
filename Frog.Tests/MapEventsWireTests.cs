using System.Collections.Generic;
using System.Text.Json;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventsWireTests
{
    [Fact]
    public void MapEventWireEntry_RoundtripJson()
    {
        var rows = new[]
        {
            new MapEventWireEntry
            {
                PlacementId = 99,
                CatalogId = 1,
                Slug = "demo_interact",
                DisplayName = "Interaction démo",
                TileX = 3,
                TileY = 7,
            },
        };

        var json = JsonSerializer.Serialize(rows);
        var back = JsonSerializer.Deserialize<List<MapEventWireEntry>>(json);
        Assert.NotNull(back);
        Assert.Single(back);
        Assert.Equal(99, back[0].PlacementId);
        Assert.Equal("demo_interact", back[0].Slug);
        Assert.Equal(7, back[0].TileY);
    }
}
