using Frog.Core.Constants;
using Xunit;

namespace Frog.Tests;

public sealed class MapEventSlugsTests
{
    [Fact]
    public void DemoInteract_slug_matches_seed_catalog()
    {
        Assert.Equal("demo_interact", MapEventSlugs.DemoInteract);
    }
}
