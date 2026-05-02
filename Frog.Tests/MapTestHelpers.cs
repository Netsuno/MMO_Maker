using Frog.Server.Config;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frog.Tests;

public static class MapTestHelpers
{
    public static MapService CreateMapService(string? worldMapPath = null)
        => new MapService(
            Options.Create(new WorldMapOptions { WorldMapPath = worldMapPath }),
            NullLogger<MapService>.Instance);
}
