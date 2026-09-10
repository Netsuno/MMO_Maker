using System;
using System.IO;
using Frog.Core.Models;
using Frog.Server.Config;
using Frog.Server.Database;
using Frog.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Frog.Core.IO;

namespace Frog.Tests;

public static class MapTestHelpers
{
    public static MapService CreateMapService(string? worldMapPath = null, IMapBlobStore? blobStore = null)
        => new MapService(
            Options.Create(new WorldMapOptions { WorldMapPath = worldMapPath }),
            Options.Create(new Phase7ContentOptions
            {
                AllowSyntheticContentFallback = true,
                RequirePublishedWorld = false,
            }),
            blobStore ?? NullMapBlobStore.Instance,
            NullLogger<MapService>.Instance);

    public static MapService CreateMapServiceFromMap(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var tmp = Path.Combine(Path.GetTempPath(), $"frog-map-test-{Guid.NewGuid():N}.fmap");
        var serializer = new MapSerializer();
        File.WriteAllBytes(tmp, serializer.Serialize(map));
        try
        {
            return CreateMapService(tmp);
        }
        finally
        {
            try
            {
                File.Delete(tmp);
            }
            catch
            {
                // tests : ignorer nettoyage fichier temporaire
            }
        }
    }
}
