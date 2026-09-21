#nullable enable
using System.IO;
using Frog.Application.Prefabs;
using Frog.Core.Protocol;

namespace Frog.Client.Assets;

/// <summary>
/// Matérialise les PNG / sidecars prefab du catalogue publié vers <c>Prefabs/</c> et <c>Maps/*.prefabs.json</c>
/// (même layout que <see cref="ClientPrefabLoader"/>).
/// </summary>
public static class ClientPublishedPrefabMaterializer
{
    public static int Materialize(PublishedCatalogWire? catalog, string appBaseDirectory)
    {
        var dirs = ClientTilesetLoader.ResolveSearchDirectories(appBaseDirectory);
        return PublishedPrefabCatalogMaterializer.Materialize(catalog, dirs);
    }
}
