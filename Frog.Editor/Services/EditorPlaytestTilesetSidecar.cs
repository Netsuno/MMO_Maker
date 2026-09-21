using System.IO;
using System.Linq;
using Frog.Application.Assets;
using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Editor.Assets;

namespace Frog.Editor.Services;

/// <summary>Écrit tilesets + prefabs au layout client avant le lancement playtest.</summary>
internal sealed class EditorPlaytestTilesetSidecar : IPlaytestAssetSidecar
{
    private readonly Func<IReadOnlyList<PrefabPlacement>>? _prefabPlacements;

    public EditorPlaytestTilesetSidecar(Func<IReadOnlyList<PrefabPlacement>>? prefabPlacements = null)
    {
        _prefabPlacements = prefabPlacements;
    }

    public void Write(PlaytestLaunchPlan plan, string? clientExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var names = plan.Maps.Select(m => m.Name).ToArray();
        var files = CollectTilesetFiles(plan);
        if (files.Count > 0)
        {
            MapTilesetPackage.WriteSidecars(plan.WorkDirectory, names, files);
            SidecarPublishedTilesetCatalog.WriteFromFiles(
                SidecarPublishedTilesetCatalog.PathBesideManifest(plan.ManifestPath),
                files,
                published: TryListPublishedTilesets());
        }

        var catalog = PrefabSpriteCache.LoadCatalog();
        var placements = _prefabPlacements?.Invoke() ?? Array.Empty<PrefabPlacement>();
        var sprites = PrefabSpriteCache.SnapshotPngFiles(catalog);
        MapPrefabPackage.WriteSidecars(plan.WorkDirectory, names, catalog, placements, sprites);

        if (string.IsNullOrWhiteSpace(clientExecutablePath))
        {
            return;
        }

        var clientDir = Path.GetDirectoryName(clientExecutablePath);
        if (string.IsNullOrWhiteSpace(clientDir))
        {
            return;
        }

        if (files.Count > 0)
        {
            MapTilesetPackage.WriteSidecars(clientDir, names, files);
        }

        MapPrefabPackage.WriteSidecars(clientDir, names, catalog, placements, sprites);
    }

    private static IReadOnlyList<MapTilesetFile> CollectTilesetFiles(PlaytestLaunchPlan plan)
    {
        var files = TilesetCache.SnapshotPngFiles().ToList();
        var have = files.Select(f => f.Id).ToHashSet();
        var maps = plan.Maps.Select(m => m.Map).ToArray();
        if (maps.Length == 0)
        {
            return files;
        }

        IReadOnlyList<TilesetDefinition> published = TryListPublishedTilesets();
        var images = new CompositePublishedTilesetImageSource(
            EmbeddedPublishedTilesetImageSource.Instance,
            new ProjectAssetTilesetImageSource(ProjectAssetRoot.Resolve()));

        foreach (var map in maps)
        {
            foreach (var file in PublishedTilesetCacheHydrator.CollectPngFiles(map, published, images))
            {
                if (have.Add(file.Id))
                {
                    files.Add(file);
                }
            }
        }

        return files;
    }

    private static IReadOnlyList<TilesetDefinition> TryListPublishedTilesets()
    {
        try
        {
            var bundle = EditorTilesetRepositoryFactory.CreateBundle();
            return bundle.PublishedCatalog.ListPublishedAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return Array.Empty<TilesetDefinition>();
        }
    }
}
