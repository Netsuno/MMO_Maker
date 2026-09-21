#nullable enable
using System.IO;
using Frog.Application.Assets;
using Frog.Core.Protocol;

namespace Frog.Client.Assets;

/// <summary>
/// Matérialise les PNG du catalogue publié vers <c>Tilesets/{paletteId}.png</c>
/// (même layout que <see cref="ClientTilesetLoader"/>).
/// </summary>
public static class ClientPublishedTilesetMaterializer
{
    public static int Materialize(PublishedCatalogWire? catalog, string appBaseDirectory, string? mapName = null)
    {
        var dirs = ClientTilesetLoader.ResolveSearchDirectories(appBaseDirectory);
        IReadOnlyList<string>? names = string.IsNullOrWhiteSpace(mapName) ? null : [mapName];
        return PublishedTilesetCatalogMaterializer.Materialize(catalog, dirs, names);
    }
}
