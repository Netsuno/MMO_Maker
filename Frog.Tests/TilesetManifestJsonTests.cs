using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class TilesetManifestJsonTests
{
    [Fact]
    public void Roundtrip_SerializeDeserialize()
    {
        var m = new TilesetManifest { ManifestVersion = 1 };
        m.Entries.Add(new TilesetManifestEntry { Id = 2, FileName = "terrain.png" });
        var bytes = TilesetManifestJson.Serialize(m);
        var back = TilesetManifestJson.TryDeserialize(bytes);
        Assert.NotNull(back);
        var e = Assert.Single(back!.Entries);
        Assert.Equal(2, e.Id);
        Assert.Equal("terrain.png", e.FileName);
    }
}
